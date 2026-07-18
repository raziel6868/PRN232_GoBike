using BusinessObjects.DTOs;
using BusinessObjects.Entities;

namespace API.Accounts;

public interface ICustomerAccountService
{
    Task<(User? User, string? Error)> RegisterAsync(
        CustomerRegistrationRequest request,
        CancellationToken cancellationToken);

    Task<User?> GetUserWithCustomerAsync(int userId, CancellationToken cancellationToken);

    Task<(User? User, string? Error)> UpdateOwnProfileAsync(
        int userId,
        CustomerProfileUpdateRequest request,
        CancellationToken cancellationToken);
}
