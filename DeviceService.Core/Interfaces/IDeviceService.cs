using DeviceService.Core.Entities;

namespace DeviceService.Core.Interfaces;

public interface IDeviceService
{
    Task<Device?> GetDeviceByIdAsync(int id);
    Task<List<Device>> GetAllDevicesAsync();
    Task<List<Device>> GetDevicesByCustomerAsync(int customerId);
    Task<Device> CreateDeviceAsync(int customerId, string brand, string model, string? serialNumber, string complaintDescription);
    Task UpdateDeviceAsync(int id, string brand, string model, string? serialNumber, string complaintDescription);
    Task DeleteDeviceAsync(int id);
}
