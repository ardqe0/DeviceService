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

    public Task SendServiceTicketAsync(ServiceTicketEmail email)
    {
        var encoder = HtmlEncoder.Default;
        return SendAsync(email.RecipientEmail, email.CustomerName, $"{email.TicketNumber} servis fişi oluşturuldu", $"""
            <div style="font-family:Arial,sans-serif;max-width:620px;margin:auto;color:#172033">
                <h2>Servis fişiniz oluşturuldu</h2>
                <p>Merhaba {encoder.Encode(email.CustomerName)},</p>
                <p><strong>{encoder.Encode(email.DeviceName)}</strong> cihazınız için <strong>{encoder.Encode(email.TicketNumber)}</strong> numaralı servis fişi oluşturuldu.</p>
                <p style="margin:28px 0"><a href="{encoder.Encode(email.TrackingUrl)}" style="background:#2563eb;color:#fff;text-decoration:none;padding:12px 18px;border-radius:6px;display:inline-block">Servis Durumunu Görüntüle</a></p>
            </div>
            """);
    }

    public Task SendPasswordResetAsync(PasswordResetEmail email)
    {
        var encoder = HtmlEncoder.Default;
        return SendAsync(email.RecipientEmail, email.RecipientName, "Şifre sıfırlama bağlantısı", $"""
            <div style="font-family:Arial,sans-serif;max-width:620px;margin:auto;color:#172033">
                <h2>Şifre sıfırlama isteği</h2>
                <p>Merhaba {encoder.Encode(email.RecipientName)},</p>
                <p>Şifrenizi sıfırlamak için aşağıdaki bağlantıyı kullanın. Bu bağlantı 30 dakika geçerlidir ve bir kez kullanılabilir.</p>
                <p style="margin:28px 0"><a href="{encoder.Encode(email.ResetUrl)}" style="background:#2563eb;color:#fff;text-decoration:none;padding:12px 18px;border-radius:6px;display:inline-block">Şifremi Sıfırla</a></p>
                <p>Bu isteği siz yapmadıysanız herhangi bir işlem yapmanız gerekmez.</p>
            </div>
            """);
    }

    public Task SendNewDeviceLoginAsync(NewDeviceLoginEmail email)
    {
        var encoder = HtmlEncoder.Default;
        var localTime = email.LoggedInAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
        return SendAsync(email.RecipientEmail, email.RecipientName, "Hesabınıza yeni cihazdan giriş yapıldı", $"""
            <div style="font-family:Arial,sans-serif;max-width:620px;margin:auto;color:#172033">
                <h2>Yeni cihaz girişi</h2>
                <p>Merhaba {encoder.Encode(email.RecipientName)},</p>
                <p>Hesabınıza yeni bir cihazdan giriş yapıldı.</p>
                <p><strong>Cihaz:</strong> {encoder.Encode(email.DeviceName)}<br /><strong>Zaman:</strong> {encoder.Encode(localTime)}</p>
                <p>Bu giriş size ait değilse şifrenizi değiştirerek tüm cihazlardaki oturumları kapatın.</p>
            </div>
            """);
    }

    private async Task SendAsync(string recipientEmail, string recipientName, string subject, string body)
    {
        ValidateOptions();
        var fromAddress = string.IsNullOrWhiteSpace(_options.FromAddress) ? _options.UserName : _options.FromAddress;
        using var message = new MailMessage { From = new MailAddress(fromAddress, _options.FromName), Subject = subject, IsBodyHtml = true, Body = body };
        message.To.Add(new MailAddress(recipientEmail, recipientName));
        using var client = new SmtpClient(_options.Host, _options.Port) { EnableSsl = _options.EnableSsl, UseDefaultCredentials = false, Credentials = new NetworkCredential(_options.UserName, _options.Password) };
        await client.SendMailAsync(message);
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.UserName) || string.IsNullOrWhiteSpace(_options.Password))
            throw new InvalidOperationException("SMTP ayarları eksik.");
    }
}
