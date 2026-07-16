namespace DeviceService.Core.Entities;

public class TrackingVerificationAttempt
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string RemoteAddress { get; set; } = string.Empty;
    public int FailedAttempts { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
