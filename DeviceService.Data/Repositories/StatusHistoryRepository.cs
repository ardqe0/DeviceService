using DeviceService.Core.Entities;
using DeviceService.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DeviceService.Data.Repositories;

public class StatusHistoryRepository : BaseRepository<StatusHistory>, IStatusHistoryRepository
{
    public StatusHistoryRepository(DeviceServiceDbContext context) : base(context)
    {
    }

    public async Task<List<StatusHistory>> GetHistoryByTicketAsync(int serviceTicketId)
    {
        return await _dbSet
            .Where(sh => sh.ServiceTicketId == serviceTicketId)
            .OrderBy(sh => sh.ChangedAt)
            .ToListAsync();
    }
}
