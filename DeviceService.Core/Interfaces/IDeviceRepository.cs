using DeviceService.Core.Entities;

namespace DeviceService.Core.Interfaces;

public interface IDeviceRepository : IRepository<Device>
{
    Task<Device?> GetDeviceWithTicketsAsync(int id);
    Task<List<Device>> GetAllDevicesAsync();
    Task<List<Device>> GetDevicesByCustomerAsync(int customerId);
}
