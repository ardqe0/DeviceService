using DeviceService.Core.Entities;

namespace DeviceService.Core.Interfaces;

public interface ITrackingLinkRepository : IRepository<TrackingLink>
{
    Task<TrackingLink?> GetByTokenAsync(string token);
    Task<TrackingLink?> GetByServiceTicketIdAsync(int serviceTicketId);
}
