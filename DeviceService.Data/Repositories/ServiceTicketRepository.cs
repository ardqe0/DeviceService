using DeviceService.Core.Entities;
using DeviceService.Core.Enums;
using DeviceService.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DeviceService.Data.Repositories;

public class ServiceTicketRepository : BaseRepository<ServiceTicket>, IServiceTicketRepository
{
    public ServiceTicketRepository(DeviceServiceDbContext context) : base(context)
    {
    }

    public async Task<ServiceTicket?> GetTicketWithHistoryAsync(int id)
    {
        return await _dbSet
            .Include(st => st.Device)
                .ThenInclude(d => d.Customer)
            .Include(st => st.StatusHistories.OrderBy(sh => sh.ChangedAt))
            .Include(st => st.TrackingLink)
            .FirstOrDefaultAsync(st => st.Id == id);
    }

    public async Task<List<ServiceTicket>> GetAllTicketsAsync()
    {
        return await _dbSet
            .Include(st => st.Device)
                .ThenInclude(d => d.Customer)
            .OrderByDescending(st => st.Id)
            .ToListAsync();
    }

    public async Task<List<ServiceTicket>> GetTicketsByStatusAsync(ServiceTicketStatus status)
    {
        return await _dbSet
            .Where(st => st.Status == status)
            .Include(st => st.Device)
                .ThenInclude(d => d.Customer)
            .Include(st => st.StatusHistories)
            .OrderByDescending(st => st.Id)
            .ToListAsync();
    }

    public async Task<List<ServiceTicket>> GetTicketsByDeviceAsync(int deviceId)
    {
        return await _dbSet
            .Where(st => st.DeviceId == deviceId)
            .Include(st => st.StatusHistories)
            .ToListAsync();
    }
}
