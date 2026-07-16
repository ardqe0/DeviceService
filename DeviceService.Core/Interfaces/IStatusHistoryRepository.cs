using DeviceService.Core.Entities;

namespace DeviceService.Core.Interfaces;

public interface IStatusHistoryRepository : IRepository<StatusHistory>
{
    Task<List<StatusHistory>> GetHistoryByTicketAsync(int serviceTicketId);
}
