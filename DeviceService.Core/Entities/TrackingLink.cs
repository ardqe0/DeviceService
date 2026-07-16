

namespace DeviceService.Core.Entities;

public class TrackingLink
{
    public int Id { get; set; }
    public int ServiceTicketId { get; set; }
    public string Token { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ExpiresAt { get; set; }

    // Navigation
    public ServiceTicket ServiceTicket { get; set; } = null!;
}
