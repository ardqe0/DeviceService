namespace DeviceService.Core.Entities;

public class UserAccount
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Customer";
    public int? CustomerId { get; set; }
    public string? BusinessName { get; set; }
    public string? TaxNumber { get; set; }
    public string? BusinessAddress { get; set; }
    public string? ContactName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
