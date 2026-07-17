namespace DeviceService.Core.Entities;

public class UserLoginDevice
{
    public int Id { get; set; }
    public int UserAccountId { get; set; }
    public string DeviceHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public UserAccount UserAccount { get; set; } = null!;
}
