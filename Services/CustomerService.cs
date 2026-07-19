using BusinessObjects;
using BusinessObjects.Entities;
using BusinessObjects.Enums;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Repositories;
using Services.DTOs;
using Services.Interfaces;

namespace Services;

public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository customerRepository;
    private readonly AppDbContext context;

    public CustomerService(ICustomerRepository customerRepository, AppDbContext context)
    {
        this.customerRepository = customerRepository;
        this.context = context;
    }

    public Task<List<Customer>> GetAllAsync()
        => customerRepository.GetAllAsync();

    public Task<Customer?> GetByIdAsync(int id)
        => customerRepository.GetByIdAsync(id);

    public async Task CreateAsync(Customer customer, string username)
    {
        ValidateCustomer(customer);

        username = username.Trim();
        customer.FullName = customer.FullName.Trim();
        customer.CCCD = customer.CCCD.Trim();
        customer.PhoneNumber = customer.PhoneNumber.Trim();
        customer.Email = string.IsNullOrWhiteSpace(customer.Email) ? null : customer.Email.Trim();
        customer.Address = string.IsNullOrWhiteSpace(customer.Address) ? null : customer.Address.Trim();
        customer.DriverLicenseNo = customer.DriverLicenseNo.Trim();

        if (await context.Users.AnyAsync(user => user.Username == username))
            throw new InvalidOperationException("Username already exists in the system");

        if (await customerRepository.ExistsByCccdAsync(customer.CCCD))
        {
            throw new InvalidOperationException("CCCD already exists in the system");
        }

        if (await customerRepository.ExistsByDriverLicenseNoAsync(customer.DriverLicenseNo))
        {
            throw new InvalidOperationException("Driver license number already exists in the system");
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(CustomerCreateDto.DefaultPassword, workFactor: 11);
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            context.Users.Add(new User
            {
                Username = username,
                PasswordHash = passwordHash,
                FullName = customer.FullName,
                Email = customer.Email,
                PhoneNumber = customer.PhoneNumber,
                Role = UserRole.Customer,
                CustomerId = customer.Id,
                IsActive = true,
                CreatedAt = SystemClock.Now
            });

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException("Unable to create the customer account because its identity or username already exists", ex);
        }
    }

    public async Task UpdateAsync(Customer customer)
    {
        ValidateCustomer(customer);

        if (await customerRepository.ExistsByCccdAsync(customer.CCCD, customer.Id))
        {
            throw new InvalidOperationException("CCCD already exists in the system");
        }

        if (await customerRepository.ExistsByDriverLicenseNoAsync(customer.DriverLicenseNo, customer.Id))
        {
            throw new InvalidOperationException("Driver license number already exists in the system");
        }

        var existing = await context.Customers
            .Include(candidate => candidate.User)
            .FirstOrDefaultAsync(candidate => candidate.Id == customer.Id)
            ?? throw new InvalidOperationException($"Customer with ID {customer.Id} not found");

        existing.FullName = customer.FullName.Trim();
        existing.CCCD = customer.CCCD.Trim();
        existing.PhoneNumber = customer.PhoneNumber.Trim();
        existing.Email = string.IsNullOrWhiteSpace(customer.Email) ? null : customer.Email.Trim();
        existing.Address = string.IsNullOrWhiteSpace(customer.Address) ? null : customer.Address.Trim();
        existing.DateOfBirth = customer.DateOfBirth.Date;
        existing.DriverLicenseNo = customer.DriverLicenseNo.Trim();
        existing.IsActive = customer.IsActive;
        existing.UpdatedAt = SystemClock.Now;

        if (existing.User != null)
        {
            existing.User.FullName = existing.FullName;
            existing.User.Email = existing.Email;
            existing.User.PhoneNumber = existing.PhoneNumber;
            existing.User.UpdatedAt = SystemClock.Now;
        }

        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
        => await DeactivateAsync(id);

    public async Task DeactivateAsync(int id)
    {
        var customer = await customerRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Customer with ID {id} not found");

        customer.IsActive = false;
        customer.UpdatedAt = SystemClock.Now;
        customerRepository.Update(customer);
    }

    public async Task ReactivateAsync(int id)
    {
        var customer = await customerRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Customer with ID {id} not found");

        customer.IsActive = true;
        customer.UpdatedAt = SystemClock.Now;
        customerRepository.Update(customer);
    }

    private static void ValidateCustomer(Customer customer)
    {
        if (string.IsNullOrWhiteSpace(customer.FullName))
        {
            throw new InvalidOperationException("FullName is required");
        }

        if (string.IsNullOrWhiteSpace(customer.CCCD) || customer.CCCD.Length != 12 || !customer.CCCD.All(char.IsDigit))
        {
            throw new InvalidOperationException("CCCD must be exactly 12 digits");
        }

        if (string.IsNullOrWhiteSpace(customer.PhoneNumber) || !System.Text.RegularExpressions.Regex.IsMatch(customer.PhoneNumber, @"^0[0-9]{9,10}$"))
        {
            throw new InvalidOperationException("Invalid Vietnamese phone format");
        }

        if (string.IsNullOrWhiteSpace(customer.DriverLicenseNo))
        {
            throw new InvalidOperationException("DriverLicenseNo is required");
        }

        var age = SystemClock.Today.Year - customer.DateOfBirth.Year -
                  (customer.DateOfBirth.Date > SystemClock.Today.AddYears(-(SystemClock.Today.Year - customer.DateOfBirth.Year)) ? 1 : 0);
        if (age < 18)
        {
            throw new InvalidOperationException("Customer must be at least 18 years old");
        }
    }
}
