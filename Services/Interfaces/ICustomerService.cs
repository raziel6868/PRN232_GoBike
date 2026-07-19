using BusinessObjects.Entities;

namespace Services.Interfaces;

public interface ICustomerService
{
    Task<List<Customer>> GetAllAsync();
    Task<Customer?> GetByIdAsync(int id);
    Task CreateAsync(Customer customer, string username);
    Task UpdateAsync(Customer customer);
    Task DeleteAsync(int id);
    Task DeactivateAsync(int id);
    Task ReactivateAsync(int id);
}
