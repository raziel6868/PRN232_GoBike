using BusinessObjects;
using BusinessObjects.DTOs;
using BusinessObjects.Entities;
using BusinessObjects.Enums;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;

namespace API.Accounts;

public sealed class CustomerAccountService : ICustomerAccountService
{
    private readonly AppDbContext context;

    public CustomerAccountService(AppDbContext context)
    {
        this.context = context;
    }

    public async Task<(User? User, string? Error)> RegisterAsync(
        CustomerRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateCustomer(request.DateOfBirth);
        if (validationError != null)
            return (null, validationError);

        var username = request.Username.Trim();
        var cccd = request.CCCD.Trim();
        var driverLicense = request.DriverLicenseNo.Trim();

        if (await context.Users.AnyAsync(user => user.Username == username, cancellationToken))
            return (null, "Username already exists.");

        var existingCustomer = await context.Customers
            .Include(customer => customer.User)
            .FirstOrDefaultAsync(customer => customer.CCCD == cccd, cancellationToken);

        if (existingCustomer?.User != null)
            return (null, "This customer profile already has a login account.");

        if (existingCustomer != null && !ExistingProfileMatches(existingCustomer, request, driverLicense))
            return (null, "The supplied details do not match the existing customer profile.");

        if (existingCustomer == null &&
            await context.Customers.AnyAsync(customer => customer.DriverLicenseNo == driverLicense, cancellationToken))
            return (null, "Driver license number already exists.");

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var customer = existingCustomer ?? new Customer
            {
                FullName = request.FullName.Trim(),
                CCCD = cccd,
                PhoneNumber = request.PhoneNumber.Trim(),
                Email = request.Email.Trim(),
                Address = NormalizeOptional(request.Address),
                DateOfBirth = request.DateOfBirth.Date,
                DriverLicenseNo = driverLicense,
                IsActive = true,
                CreatedAt = SystemClock.Now
            };

            if (existingCustomer == null)
            {
                context.Customers.Add(customer);
                await context.SaveChangesAsync(cancellationToken);
            }

            var user = new User
            {
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 11),
                FullName = customer.FullName,
                Email = customer.Email,
                PhoneNumber = customer.PhoneNumber,
                Role = UserRole.Customer,
                CustomerId = customer.Id,
                Customer = customer,
                IsActive = true,
                CreatedAt = SystemClock.Now
            };

            context.Users.Add(user);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (user, null);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (null, "The username or customer identity is already registered.");
        }
    }

    public Task<User?> GetUserWithCustomerAsync(int userId, CancellationToken cancellationToken)
        => context.Users
            .AsNoTracking()
            .Include(user => user.Customer)
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public async Task<(User? User, string? Error)> UpdateOwnProfileAsync(
        int userId,
        CustomerProfileUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateCustomer(request.DateOfBirth);
        if (validationError != null)
            return (null, validationError);

        var user = await context.Users
            .Include(candidate => candidate.Customer)
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user?.Role != UserRole.Customer || user.Customer == null)
            return (null, "Customer profile not found.");

        var cccd = request.CCCD.Trim();
        var driverLicense = request.DriverLicenseNo.Trim();
        if (await context.Customers.AnyAsync(
                customer => customer.Id != user.CustomerId && customer.CCCD == cccd,
                cancellationToken))
            return (null, "CCCD already exists.");

        if (await context.Customers.AnyAsync(
                customer => customer.Id != user.CustomerId && customer.DriverLicenseNo == driverLicense,
                cancellationToken))
            return (null, "Driver license number already exists.");

        var customerProfile = user.Customer;
        customerProfile.FullName = request.FullName.Trim();
        customerProfile.CCCD = cccd;
        customerProfile.PhoneNumber = request.PhoneNumber.Trim();
        customerProfile.Email = request.Email.Trim();
        customerProfile.Address = NormalizeOptional(request.Address);
        customerProfile.DateOfBirth = request.DateOfBirth.Date;
        customerProfile.DriverLicenseNo = driverLicense;
        customerProfile.UpdatedAt = SystemClock.Now;

        user.FullName = customerProfile.FullName;
        user.Email = customerProfile.Email;
        user.PhoneNumber = customerProfile.PhoneNumber;
        user.UpdatedAt = SystemClock.Now;

        await context.SaveChangesAsync(cancellationToken);
        return (user, null);
    }

    private static string? ValidateCustomer(DateTime dateOfBirth)
    {
        if (dateOfBirth.Date > SystemClock.Today)
            return "Date of birth cannot be in the future.";

        var age = SystemClock.Today.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > SystemClock.Today.AddYears(-age))
            age--;

        return age < 18 ? "Customer must be at least 18 years old." : null;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool ExistingProfileMatches(
        Customer customer,
        CustomerRegistrationRequest request,
        string driverLicense)
        => customer.IsActive &&
           customer.DateOfBirth.Date == request.DateOfBirth.Date &&
           string.Equals(customer.PhoneNumber, request.PhoneNumber.Trim(), StringComparison.OrdinalIgnoreCase) &&
           string.Equals(customer.Email, request.Email.Trim(), StringComparison.OrdinalIgnoreCase) &&
           string.Equals(customer.DriverLicenseNo, driverLicense, StringComparison.OrdinalIgnoreCase);
}
