namespace DeviceService.Core.Entities;

public class PasswordResetToken
{
    public int Id { get; set; }
    public int UserAccountId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public UserAccount UserAccount { get; set; } = null!;
}
