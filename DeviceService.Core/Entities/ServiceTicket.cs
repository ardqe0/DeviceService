using DeviceService.Core.Enums;

namespace DeviceService.Core.Entities;

public class ServiceTicket
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public ServiceTicketStatus Status { get; set; } = ServiceTicketStatus.TeslimAlindi;
    public decimal? EstimatedPrice { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Device Device { get; set; } = null!;
    public ICollection<StatusHistory> StatusHistories { get; set; } = new List<StatusHistory>();
    public TrackingLink? TrackingLink { get; set; }
}
