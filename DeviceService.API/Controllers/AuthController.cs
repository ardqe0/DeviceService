using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DeviceService.API.Services;
using DeviceService.Core.Entities;
using DeviceService.Core.Interfaces;
using DeviceService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace DeviceService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private const int PasswordResetMinutes = 30;
    private readonly IConfiguration _configuration;
    private readonly DeviceServiceDbContext _context;
    private readonly ITrackingService _trackingService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IConfiguration configuration, DeviceServiceDbContext context, ITrackingService trackingService, IEmailSender emailSender, ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _context = context;
        _trackingService = trackingService;
        _emailSender = emailSender;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "E-posta ve şifre zorunludur." });

        var email = request.Email.Trim().ToLowerInvariant();
        var account = await _context.UserAccounts.FirstOrDefaultAsync(x => x.Email == email);
        if (account is null || !VerifyPassword(request.Password, account.PasswordHash))
            return Unauthorized(new { message = "Geçersiz e-posta veya şifre." });

        var isNewDevice = await RegisterLoginDeviceAsync(account);
        account.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        if (isNewDevice)
            await SendNewDeviceNotificationSafelyAsync(account);

        return Ok(CreateLoginResponse(account));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            if (request is null)
                return BadRequest(new { message = "Kayıt bilgileri gönderilmedi." });

            var email = request.Email.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Ad soyad, e-posta, telefon ve şifre zorunludur." });
            if (!System.Net.Mail.MailAddress.TryCreate(email, out _))
                return BadRequest(new { message = "Geçerli bir e-posta adresi girin." });
            if (!IsValidPassword(request.Password))
                return BadRequest(new { message = "Şifre en az 8 karakter olmalı ve harf ile rakam içermelidir." });
            if (await _context.UserAccounts.AnyAsync(x => x.Email == email))
                return Conflict(new { message = "Bu e-posta ile kayıtlı bir hesap var." });

            var isService = request.IsService;
            if (isService && !IsValidServiceRegistrationCode(request.ServiceRegistrationCode))
                return BadRequest(new { message = "Servis hesabı oluşturmak için geçerli servis kayıt kodunu girin." });
            if (isService && (string.IsNullOrWhiteSpace(request.BusinessName) || string.IsNullOrWhiteSpace(request.TaxNumber) || string.IsNullOrWhiteSpace(request.BusinessAddress) || string.IsNullOrWhiteSpace(request.ContactName)))
                return BadRequest(new { message = "Servis kaydında işyeri bilgileri eksiksiz girilmelidir." });

            int? customerId = null;
            if (!isService)
            {
                var nameParts = request.FullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                var customer = new Customer { FirstName = nameParts[0], LastName = nameParts.Length > 1 ? nameParts[1] : string.Empty, Email = email, PhoneNumber = request.PhoneNumber.Trim() };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
                customerId = customer.Id;
            }

            var account = new UserAccount
            {
                FullName = request.FullName.Trim(),
                Email = email,
                PhoneNumber = request.PhoneNumber.Trim(),
                PasswordHash = HashPassword(request.Password),
                Role = isService ? "Service" : "Customer",
                CustomerId = customerId,
                BusinessName = isService ? request.BusinessName?.Trim() : null,
                TaxNumber = isService ? request.TaxNumber?.Trim() : null,
                BusinessAddress = isService ? request.BusinessAddress?.Trim() : null,
                ContactName = isService ? request.ContactName?.Trim() : null
            };
            _context.UserAccounts.Add(account);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(CreateLoginResponse(account));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Kayıt oluşturulurken hata oluştu.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Kayıt oluşturulurken bir hata oluştu." });
        }
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        const string responseMessage = "E-posta adresi kayıtlıysa şifre sıfırlama bağlantısı gönderildi.";
        if (request is null || string.IsNullOrWhiteSpace(request.Email))
            return Ok(new { message = responseMessage });

        var email = request.Email.Trim().ToLowerInvariant();
        var account = await _context.UserAccounts.FirstOrDefaultAsync(x => x.Email == email);
        if (account is null)
            return Ok(new { message = responseMessage });

        var now = DateTime.UtcNow;
        var activeTokens = await _context.PasswordResetTokens.Where(x => x.UserAccountId == account.Id && x.UsedAt == null).ToListAsync();
        foreach (var activeToken in activeTokens)
            activeToken.UsedAt = now;

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        _context.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserAccountId = account.Id,
            TokenHash = HashOpaqueToken(rawToken),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(PasswordResetMinutes)
        });
        await _context.SaveChangesAsync();

        var publicBaseUrl = _configuration["Email:PublicAppBaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            _logger.LogWarning("Şifre sıfırlama e-postası gönderilemedi: Email:PublicAppBaseUrl tanımlı değil.");
            return Ok(new { message = responseMessage });
        }

        var resetUrl = $"{publicBaseUrl}/sifre-sifirla?token={Uri.EscapeDataString(rawToken)}";
        try
        {
            await _emailSender.SendPasswordResetAsync(new PasswordResetEmail(account.Email, account.FullName, resetUrl));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Şifre sıfırlama e-postası gönderilemedi. AccountId: {AccountId}", account.Id);
        }

        return Ok(new { message = responseMessage });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Token) || !IsValidPassword(request.NewPassword))
            return BadRequest(new { message = "Geçerli bağlantı ve güçlü bir şifre girin." });

        var now = DateTime.UtcNow;
        var tokenHash = HashOpaqueToken(request.Token);
        var resetToken = await _context.PasswordResetTokens.Include(x => x.UserAccount)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash && x.UsedAt == null && x.ExpiresAt > now);
        if (resetToken is null)
            return BadRequest(new { message = "Şifre sıfırlama bağlantısı geçersiz veya süresi dolmuş." });

        resetToken.UsedAt = now;
        resetToken.UserAccount.PasswordHash = HashPassword(request.NewPassword);
        resetToken.UserAccount.SessionVersion++;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Şifreniz değiştirildi. Yeni şifrenizle giriş yapabilirsiniz." });
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.CurrentPassword) || !IsValidPassword(request.NewPassword))
            return BadRequest(new { message = "Mevcut şifrenizi ve güçlü yeni şifrenizi girin." });
        if (request.CurrentPassword == request.NewPassword)
            return BadRequest(new { message = "Yeni şifre mevcut şifreden farklı olmalıdır." });

        var account = await GetCurrentAccountAsync();
        if (account is null)
            return Unauthorized();
        if (!VerifyPassword(request.CurrentPassword, account.PasswordHash))
            return BadRequest(new { message = "Mevcut şifre doğru değil." });

        account.PasswordHash = HashPassword(request.NewPassword);
        account.SessionVersion++;
        await _context.SaveChangesAsync();
        return Ok(CreateLoginResponse(account));
    }

    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll()
    {
        var account = await GetCurrentAccountAsync();
        if (account is null)
            return Unauthorized();

        account.SessionVersion++;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles = "Customer")]
    [HttpGet("my-tickets")]
    public async Task<IActionResult> GetMyTickets()
    {
        var customerIdText = User.FindFirstValue("customerId");
        if (!int.TryParse(customerIdText, out var customerId)) return Forbid();
        var customer = await _context.Customers.FindAsync(customerId);
        if (customer is null) return NotFound();

        var ticketEntities = await _context.ServiceTickets
            .Include(ticket => ticket.Device)
            .Include(ticket => ticket.StatusHistories)
            .Where(ticket => ticket.Device.CustomerId == customerId)
            .OrderByDescending(ticket => ticket.Id)
            .ToListAsync();

        var tickets = new List<CustomerTicketDto>();
        foreach (var ticket in ticketEntities)
        {
            var trackingLink = await _trackingService.CreateOrGetTrackingLinkAsync(ticket.Id);
            tickets.Add(new CustomerTicketDto
            {
                Id = ticket.Id,
                TicketNumber = $"SF-{ticket.Id:000000}",
                DeviceName = ticket.Device.Model,
                Brand = ticket.Device.Brand,
                SerialNumber = ticket.Device.SerialNumber,
                CreatedDate = ticket.CreatedAt,
                Status = GetStatusText(ticket.Status),
                EstimatedPrice = ticket.EstimatedPrice,
                TrackingToken = trackingLink.Token,
                StatusHistories = ticket.StatusHistories.OrderByDescending(history => history.ChangedAt).Select(history => new CustomerTicketStatusHistoryDto { Status = GetStatusText(history.Status), ChangedAt = history.ChangedAt, Notes = history.Notes }).ToList()
            });
        }

        return Ok(new CustomerDashboardDto { FullName = customer.FirstName + " " + customer.LastName, Email = customer.Email ?? string.Empty, PhoneNumber = customer.PhoneNumber, Tickets = tickets });
    }

    private async Task<UserAccount?> GetCurrentAccountAsync()
    {
        var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdText, out var userId) ? await _context.UserAccounts.FindAsync(userId) : null;
    }

    private async Task<bool> RegisterLoginDeviceAsync(UserAccount account)
    {
        var suppliedId = Request.Headers["X-Device-Id"].FirstOrDefault();
        var userAgent = Request.Headers.UserAgent.ToString();
        var fingerprint = string.IsNullOrWhiteSpace(suppliedId)
            ? $"fallback|{userAgent}|{HttpContext.Connection.RemoteIpAddress}"
            : suppliedId.Trim();
        var hash = HashOpaqueToken(fingerprint);
        var now = DateTime.UtcNow;
        var device = await _context.UserLoginDevices.FirstOrDefaultAsync(x => x.UserAccountId == account.Id && x.DeviceHash == hash);
        if (device is not null)
        {
            device.LastSeenAt = now;
            return false;
        }

        var displayName = Request.Headers["X-Device-Name"].FirstOrDefault();
        displayName = string.IsNullOrWhiteSpace(displayName) ? userAgent : displayName;
        _context.UserLoginDevices.Add(new UserLoginDevice
        {
            UserAccountId = account.Id,
            DeviceHash = hash,
            DisplayName = Truncate(displayName, 180),
            FirstSeenAt = now,
            LastSeenAt = now
        });
        return true;
    }

    private async Task SendNewDeviceNotificationSafelyAsync(UserAccount account)
    {
        try
        {
            var deviceName = Request.Headers["X-Device-Name"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(deviceName))
                deviceName = Request.Headers.UserAgent.ToString();
            await _emailSender.SendNewDeviceLoginAsync(new NewDeviceLoginEmail(account.Email, account.FullName, Truncate(deviceName, 180), DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Yeni cihaz giriş bildirimi gönderilemedi. AccountId: {AccountId}", account.Id);
        }
    }

    private object CreateLoginResponse(UserAccount account) => new
    {
        token = GenerateJwtToken(account),
        user = new { id = account.Id, fullName = account.FullName, email = account.Email, role = account.Role, customerId = account.CustomerId }
    };

    private string GenerateJwtToken(UserAccount account)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expirationMinutes = int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "60");
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new Claim(ClaimTypes.Name, account.FullName),
            new Claim(ClaimTypes.Email, account.Email),
            new Claim(ClaimTypes.Role, account.Role),
            new Claim("customerId", account.CustomerId?.ToString() ?? string.Empty),
            new Claim("sv", account.SessionVersion.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var token = new JwtSecurityToken(_configuration["Jwt:Issuer"], _configuration["Jwt:Audience"], claims, expires: DateTime.UtcNow.AddMinutes(expirationMinutes), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private bool IsValidServiceRegistrationCode(string? suppliedCode)
    {
        var configuredCode = _configuration["Registration:ServiceCode"];
        if (string.IsNullOrWhiteSpace(configuredCode) || string.IsNullOrWhiteSpace(suppliedCode)) return false;
        var expectedBytes = Encoding.UTF8.GetBytes(configuredCode);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedCode);
        return expectedBytes.Length == suppliedBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }

    private static bool IsValidPassword(string? password) => !string.IsNullOrWhiteSpace(password) && password.Length >= 8 && password.Any(char.IsLetter) && password.Any(char.IsDigit);
    private static string Truncate(string? value, int length) => string.IsNullOrWhiteSpace(value) ? "Bilinmeyen cihaz" : value.Trim()[..Math.Min(value.Trim().Length, length)];
    private static string HashOpaqueToken(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split('.', 2);
        if (parts.Length != 2) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[0]);
            var expectedHash = Convert.FromBase64String(parts[1]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210000, HashAlgorithmName.SHA256, expectedHash.Length);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch { return false; }
    }

    private static string GetStatusText(DeviceService.Core.Enums.ServiceTicketStatus status) => status switch
    {
        DeviceService.Core.Enums.ServiceTicketStatus.TeslimAlindi => "Teslim Alındı",
        DeviceService.Core.Enums.ServiceTicketStatus.İncelemede => "İncelemede",
        DeviceService.Core.Enums.ServiceTicketStatus.ParcaBeklenıyor => "Parça Bekleniyor",
        DeviceService.Core.Enums.ServiceTicketStatus.TamirEdiliyor => "Tamir Ediliyor",
        DeviceService.Core.Enums.ServiceTicketStatus.TestEdiliyor => "Test Ediliyor",
        DeviceService.Core.Enums.ServiceTicketStatus.Hazir => "Hazır",
        DeviceService.Core.Enums.ServiceTicketStatus.TeslimEdildi => "Teslim Edildi",
        DeviceService.Core.Enums.ServiceTicketStatus.İptal => "İptal",
        _ => status.ToString()
    };
}

public class LoginRequest { public string Email { get; set; } = string.Empty; public string Password { get; set; } = string.Empty; }
public class RegisterRequest { public string FullName { get; set; } = string.Empty; public string Email { get; set; } = string.Empty; public string PhoneNumber { get; set; } = string.Empty; public string Password { get; set; } = string.Empty; public bool IsService { get; set; } public string? BusinessName { get; set; } public string? TaxNumber { get; set; } public string? BusinessAddress { get; set; } public string? ContactName { get; set; } public string? ServiceRegistrationCode { get; set; } }
public class ForgotPasswordRequest { public string Email { get; set; } = string.Empty; }
public class ResetPasswordRequest { public string Token { get; set; } = string.Empty; public string NewPassword { get; set; } = string.Empty; }
public class ChangePasswordRequest { public string CurrentPassword { get; set; } = string.Empty; public string NewPassword { get; set; } = string.Empty; }
public class CustomerDashboardDto { public string FullName { get; set; } = string.Empty; public string Email { get; set; } = string.Empty; public string PhoneNumber { get; set; } = string.Empty; public List<CustomerTicketDto> Tickets { get; set; } = new(); }
public class CustomerTicketDto { public int Id { get; set; } public string TicketNumber { get; set; } = string.Empty; public string DeviceName { get; set; } = string.Empty; public string Brand { get; set; } = string.Empty; public string? SerialNumber { get; set; } public DateTime CreatedDate { get; set; } public string Status { get; set; } = string.Empty; public decimal? EstimatedPrice { get; set; } public string TrackingToken { get; set; } = string.Empty; public List<CustomerTicketStatusHistoryDto> StatusHistories { get; set; } = new(); }
public class CustomerTicketStatusHistoryDto { public string Status { get; set; } = string.Empty; public DateTime ChangedAt { get; set; } public string? Notes { get; set; } }
