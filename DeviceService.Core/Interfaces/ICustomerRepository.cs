using DeviceService.Core.Entities;

namespace DeviceService.Core.Interfaces;

public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer?> GetCustomerWithDevicesAsync(int id);
    Task<List<Customer>> GetAllWithDevicesAsync();
}
