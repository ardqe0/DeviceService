using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;

namespace DeviceService.API.Services;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;

    public SmtpEmailSender(IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendServiceTicketAsync(ServiceTicketEmail email)
    {
        ValidateOptions();

        var encoder = HtmlEncoder.Default;
        var fromAddress = string.IsNullOrWhiteSpace(_options.FromAddress)
            ? _options.UserName
            : _options.FromAddress;

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, _options.FromName),
            Subject = $"{email.TicketNumber} servis fişi oluşturuldu",
            IsBodyHtml = true,
            Body = $"""
                <div style="font-family:Arial,sans-serif;max-width:620px;margin:auto;color:#172033">
                    <h2>Servis fişiniz oluşturuldu</h2>
                    <p>Merhaba {encoder.Encode(email.CustomerName)},</p>
                    <p><strong>{encoder.Encode(email.DeviceName)}</strong> cihazınız için
                    <strong>{encoder.Encode(email.TicketNumber)}</strong> numaralı servis fişi oluşturuldu.</p>
                    <p style="margin:28px 0">
                        <a href="{encoder.Encode(email.TrackingUrl)}"
                           style="background:#2563eb;color:#fff;text-decoration:none;padding:12px 18px;border-radius:6px;display:inline-block">
                            Servis Durumunu Görüntüle
                        </a>
                    </p>
                    <p>Bağlantı açıldığında güvenlik için kayıtlı telefon numaranızın son dört hanesi istenir.</p>
                </div>
                """
        };
        message.To.Add(new MailAddress(email.RecipientEmail, email.CustomerName));

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_options.UserName, _options.Password)
        };

        await client.SendMailAsync(message);
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.Host) ||
            string.IsNullOrWhiteSpace(_options.UserName) ||
            string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new InvalidOperationException("SMTP ayarları eksik. Güvenli yapılandırma üzerinden SMTP bilgilerini tanımlayın.");
        }
    }
}
