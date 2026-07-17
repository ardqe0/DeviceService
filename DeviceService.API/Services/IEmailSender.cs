namespace DeviceService.API.Services;

public interface IEmailSender
{
    Task SendServiceTicketAsync(ServiceTicketEmail email);
    Task SendPasswordResetAsync(PasswordResetEmail email);
    Task SendNewDeviceLoginAsync(NewDeviceLoginEmail email);
}

public sealed record ServiceTicketEmail(string RecipientEmail, string CustomerName, string TicketNumber, string DeviceName, string TrackingUrl);
public sealed record PasswordResetEmail(string RecipientEmail, string RecipientName, string ResetUrl);
public sealed record NewDeviceLoginEmail(string RecipientEmail, string RecipientName, string DeviceName, DateTime LoggedInAtUtc);
