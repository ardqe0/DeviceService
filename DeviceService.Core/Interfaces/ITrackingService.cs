using DeviceService.Core.Entities;

namespace DeviceService.Core.Interfaces;

public interface ITrackingService
{
    Task<TrackingLink?> GetTrackingLinkByTokenAsync(string token);
    Task<TrackingLink> CreateOrGetTrackingLinkAsync(int serviceTicketId);
    Task<string> GenerateTrackingLinkAsync(int serviceTicketId);
}
