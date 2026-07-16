using DeviceService.Core;

namespace DeviceService.Core.Entities;

public class Device
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string Brand { get; set; } = null!;
    public string Model { get; set; } = null!;
    public string? SerialNumber { get; set; }
    public string ComplaintDescription { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation
    public Customer Customer { get; set; } = null!;
    public ICollection<ServiceTicket> ServiceTickets { get; set; } = new List<ServiceTicket>();
}
