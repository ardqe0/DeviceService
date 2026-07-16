using DeviceService.Core.Entities;
using DeviceService.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DeviceService.Data.Repositories;

public class DeviceRepository : BaseRepository<Device>, IDeviceRepository
{
    public DeviceRepository(DeviceServiceDbContext context) : base(context)
    {
    }

    public async Task<Device?> GetDeviceWithTicketsAsync(int id)
    {
        return await _dbSet
            .Include(d => d.ServiceTickets)
            .ThenInclude(st => st.StatusHistories)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<List<Device>> GetAllDevicesAsync()
    {
        return await _dbSet
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Device>> GetDevicesByCustomerAsync(int customerId)
    {
        return await _dbSet
            .Where(d => d.CustomerId == customerId)
            .Include(d => d.ServiceTickets)
            .ToListAsync();
    }
}
