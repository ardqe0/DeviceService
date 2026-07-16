using DeviceService.Core.Entities;

namespace DeviceService.Core.Interfaces;

public interface ICustomerService
{
    Task<Customer?> GetCustomerByIdAsync(int id);
    Task<List<Customer>> GetAllCustomersAsync();
    Task<Customer> CreateCustomerAsync(string firstName, string lastName, string phoneNumber, string? email);
    Task UpdateCustomerAsync(int id, string firstName, string lastName, string phoneNumber, string? email);
    Task DeleteCustomerAsync(int id);
}
