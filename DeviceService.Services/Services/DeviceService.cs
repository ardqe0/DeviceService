using DeviceService.Core.Entities;
using DeviceService.Core.Interfaces;

namespace DeviceService.Services;

public class DeviceService : IDeviceService
{
    private readonly IDeviceRepository _deviceRepository;

    public DeviceService(IDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<Device?> GetDeviceByIdAsync(int id)
    {
        return await _deviceRepository.GetByIdAsync(id);
    }

    public async Task<List<Device>> GetAllDevicesAsync()
    {
        return await _deviceRepository.GetAllDevicesAsync();
    }

    public async Task<List<Device>> GetDevicesByCustomerAsync(int customerId)
    {
        return await _deviceRepository.GetDevicesByCustomerAsync(customerId);
    }

    public async Task<Device> CreateDeviceAsync(int customerId, string brand, string model, string? serialNumber, string complaintDescription)
    {
        var device = new Device
        {
            CustomerId = customerId,
            Brand = brand,
            Model = model,
            SerialNumber = serialNumber,
            ComplaintDescription = complaintDescription,
            CreatedAt = DateTime.Now
        };

        await _deviceRepository.AddAsync(device);
        return device;
    }

    public async Task UpdateDeviceAsync(int id, string brand, string model, string? serialNumber, string complaintDescription)
    {
        var device = await _deviceRepository.GetByIdAsync(id);
        if (device == null)
            throw new KeyNotFoundException($"Device with ID {id} not found");

        device.Brand = brand;
        device.Model = model;
        device.SerialNumber = serialNumber;
        device.ComplaintDescription = complaintDescription;

        _deviceRepository.Update(device);
        await _deviceRepository.SaveChangesAsync();
    }

    public async Task DeleteDeviceAsync(int id)
    {
        var device = await _deviceRepository.GetByIdAsync(id);
        if (device == null)
            throw new KeyNotFoundException($"Device with ID {id} not found");

        _deviceRepository.Delete(device);
        await _deviceRepository.SaveChangesAsync();
    }
}
