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

    public async Task<(User? User, string? Error)> UpdateInternalProfileAsync(
        int userId,
        InternalProfileUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user == null || user.Role is not (UserRole.Staff or UserRole.Admin))
            return (null, "Staff or admin profile not found.");

        user.FullName = request.FullName.Trim();
        user.Email = request.Email.Trim();
        user.UpdatedAt = SystemClock.Now;

        await context.SaveChangesAsync(cancellationToken);
        return (user, null);
    }

    public async Task<(bool Success, string? Error)> ChangePasswordAsync(
        int userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user == null)
            return (false, "User not found.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return (false, "Current password is incorrect.");

        if (BCrypt.Net.BCrypt.Verify(request.NewPassword, user.PasswordHash))
            return (false, "New password must be different from the current password.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 11);
        user.UpdatedAt = SystemClock.Now;

        await context.SaveChangesAsync(cancellationToken);
        return (true, null);
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

}
