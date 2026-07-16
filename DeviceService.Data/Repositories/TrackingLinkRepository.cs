using DeviceService.Core.Entities;
using DeviceService.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DeviceService.Data.Repositories;

public class TrackingLinkRepository : BaseRepository<TrackingLink>, ITrackingLinkRepository
{
    public TrackingLinkRepository(DeviceServiceDbContext context) : base(context)
    {
    }

    public async Task<TrackingLink?> GetByTokenAsync(string token)
    {
        return await _dbSet.Include(tl => tl.ServiceTicket)
            .FirstOrDefaultAsync(tl => tl.Token == token);
    }

    public async Task<TrackingLink?> GetByServiceTicketIdAsync(int serviceTicketId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(tl => tl.ServiceTicketId == serviceTicketId);
    }
}
