using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using DeviceService.Data;
using DeviceService.Core.Entities;
using DeviceService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;

namespace DeviceService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly DeviceServiceDbContext _context;
    private readonly ITrackingService _trackingService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IConfiguration configuration,
        DeviceServiceDbContext context,
        ITrackingService trackingService,
        ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _context = context;
        _trackingService = trackingService;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "E-posta ve şifre zorunludur." });

            var email = request.Email.Trim().ToLowerInvariant();
            var account = await _context.UserAccounts
                .AsNoTracking()
                .OrderByDescending(account => account.Id)
                .FirstOrDefaultAsync(account => account.Email == email);

            if (account == null || !VerifyPassword(request.Password, account.PasswordHash))
                return Unauthorized(new { message = "Geçersiz e-posta veya şifre." });

            return Ok(CreateLoginResponse(account.Id, account.FullName, account.Email, account.Role, account.CustomerId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Giriş işlemi sırasında hata oluştu.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Giriş işlemi sırasında bir hata oluştu." });
        }
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            if (request == null)
                return BadRequest(new { message = "Kayıt bilgileri gönderilmedi." });

            var email = request.Email.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Ad soyad, e-posta, telefon ve şifre zorunludur." });

            if (!System.Net.Mail.MailAddress.TryCreate(email, out _))
                return BadRequest(new { message = "Geçerli bir e-posta adresi girin." });

            if (request.Password.Length < 8)
                return BadRequest(new { message = "Şifre en az 8 karakter olmalıdır." });

            if (await _context.UserAccounts.AnyAsync(x => x.Email == email))
                return Conflict(new { message = "Bu e-posta ile kayıtlı bir hesap var." });

            var isService = request.IsService;
            if (isService && !IsValidServiceRegistrationCode(request.ServiceRegistrationCode))
                return BadRequest(new { message = "Servis hesabı oluşturmak için geçerli servis kayıt kodunu girin." });
            if (isService && (string.IsNullOrWhiteSpace(request.BusinessName) || string.IsNullOrWhiteSpace(request.TaxNumber) ||
                string.IsNullOrWhiteSpace(request.BusinessAddress) || string.IsNullOrWhiteSpace(request.ContactName)))
                return BadRequest(new { message = "Servis kaydında işyeri bilgileri eksiksiz girilmelidir." });

            int? customerId = null;
            if (!isService)
            {
                var nameParts = request.FullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                var customer = new Customer
                {
                    FirstName = nameParts[0],
                    LastName = nameParts.Length > 1 ? nameParts[1] : string.Empty,
                    Email = email,
                    PhoneNumber = request.PhoneNumber.Trim()
                };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
                customerId = customer.Id;
            }

            var account = new UserAccount
            {
                FullName = request.FullName.Trim(), Email = email, PhoneNumber = request.PhoneNumber.Trim(),
                PasswordHash = HashPassword(request.Password), Role = isService ? "Service" : "Customer",
                CustomerId = customerId, BusinessName = isService ? request.BusinessName?.Trim() : null,
                TaxNumber = isService ? request.TaxNumber?.Trim() : null,
                BusinessAddress = isService ? request.BusinessAddress?.Trim() : null,
                ContactName = isService ? request.ContactName?.Trim() : null
            };
            _context.UserAccounts.Add(account);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(CreateLoginResponse(account.Id, account.FullName, account.Email, account.Role, account.CustomerId));
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Kayıt oluşturulurken veritabanı hatası oluştu.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Kayıt oluşturulurken veritabanı hatası oluştu." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Kayıt oluşturulurken hata oluştu.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Kayıt oluşturulurken bir hata oluştu." });
        }
    }

    [Authorize(Roles = "Customer")]
    [HttpGet("my-tickets")]
    public async Task<IActionResult> GetMyTickets()
    {
        var customerIdText = User.FindFirstValue("customerId");
        if (!int.TryParse(customerIdText, out var customerId)) return Forbid();
        var customer = await _context.Customers.FindAsync(customerId);
        if (customer == null) return NotFound();

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
                StatusHistories = ticket.StatusHistories
                    .OrderByDescending(history => history.ChangedAt)
                    .Select(history => new CustomerTicketStatusHistoryDto
                    {
                        Status = GetStatusText(history.Status),
                        ChangedAt = history.ChangedAt,
                        Notes = history.Notes
                    })
                    .ToList()
            });

        }
        return Ok(new CustomerDashboardDto { FullName = customer.FirstName + " " + customer.LastName, Email = customer.Email ?? string.Empty, PhoneNumber = customer.PhoneNumber, Tickets = tickets });
    }

    private static string GetStatusText(DeviceService.Core.Enums.ServiceTicketStatus status)
    {
        return status switch
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

    private object CreateLoginResponse
(int id, string fullName, string email, string role, int? customerId)
        => new { token = GenerateJwtToken(id, fullName, email, role, customerId), user = new { id, fullName, email, role, customerId } };

    private string GenerateJwtToken(int id, string fullName, string email, string role, int? customerId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expirationMinutes = int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "60");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Name, fullName),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role),
            new Claim("customerId", customerId?.ToString() ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Sub, id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private bool IsValidServiceRegistrationCode(string? suppliedCode)
    {
        var configuredCode = _configuration["Registration:ServiceCode"];
        if (string.IsNullOrWhiteSpace(configuredCode) || string.IsNullOrWhiteSpace(suppliedCode))
            return false;

        var expectedBytes = Encoding.UTF8.GetBytes(configuredCode);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedCode);
        return expectedBytes.Length == suppliedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
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
        catch (Exception) { return false; }
    }
}

public class LoginRequest
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
}

public class RegisterRequest { public string FullName { get; set; } = string.Empty; public string Email { get; set; } = string.Empty; public string PhoneNumber { get; set; } = string.Empty; public string Password { get; set; } = string.Empty; public bool IsService { get; set; } public string? BusinessName { get; set; } public string? TaxNumber { get; set; } public string? BusinessAddress { get; set; } public string? ContactName { get; set; } public string? ServiceRegistrationCode { get; set; } }
public class CustomerDashboardDto { public string FullName { get; set; } = string.Empty; public string Email { get; set; } = string.Empty; public string PhoneNumber { get; set; } = string.Empty; public List<CustomerTicketDto> Tickets { get; set; } = new(); }
public class CustomerTicketDto { public int Id { get; set; } public string TicketNumber { get; set; } = string.Empty; public string DeviceName { get; set; } = string.Empty; public string Brand { get; set; } = string.Empty; public string? SerialNumber { get; set; } public DateTime CreatedDate { get; set; } public string Status { get; set; } = string.Empty; public decimal? EstimatedPrice { get; set; } public string TrackingToken { get; set; } = string.Empty; public List<CustomerTicketStatusHistoryDto> StatusHistories { get; set; } = new(); }
public class CustomerTicketStatusHistoryDto { public string Status { get; set; } = string.Empty; public DateTime ChangedAt { get; set; } public string? Notes { get; set; } }
