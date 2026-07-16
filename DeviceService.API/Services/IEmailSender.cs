namespace DeviceService.API.Services;

public interface IEmailSender
{
    Task SendServiceTicketAsync(ServiceTicketEmail email);
}

public sealed record ServiceTicketEmail(
    string RecipientEmail,
    string CustomerName,
    string TicketNumber,
    string DeviceName,
    string TrackingUrl);
