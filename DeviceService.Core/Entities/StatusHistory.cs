using DeviceService.Core.Enums;

namespace DeviceService.Core.Entities;

public class StatusHistory
{
    public int Id { get; set; }
    public int ServiceTicketId { get; set; }
    public ServiceTicketStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.Now;

    // Navigation
    public ServiceTicket ServiceTicket { get; set; } = null!;
}
