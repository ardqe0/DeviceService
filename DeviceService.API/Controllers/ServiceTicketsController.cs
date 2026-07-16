using DeviceService.API.DTOs;
using DeviceService.API.Services;
using DeviceService.Data;
using DeviceService.Core.Entities;
using DeviceService.Core.Enums;
using DeviceService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DeviceService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Service")]
public class ServiceTicketsController : ControllerBase
{
    private readonly IServiceTicketService _ticketService;
    private readonly IDeviceService _deviceService;
    private readonly ICustomerService _customerService;
    private readonly ITrackingService _trackingService;
    private readonly IEmailSender _emailSender;
    private readonly DeviceServiceDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServiceTicketsController> _logger;

    public ServiceTicketsController(
        IServiceTicketService ticketService,
        IDeviceService deviceService,
        ICustomerService customerService,
        ITrackingService trackingService,
        IEmailSender emailSender,
        DeviceServiceDbContext context,
        IConfiguration configuration,
        ILogger<ServiceTicketsController> logger)
    {
        _ticketService = ticketService;
        _deviceService = deviceService;
        _customerService = customerService;
        _trackingService = trackingService;
        _emailSender = emailSender;
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var tickets = await _ticketService.GetAllTicketsAsync();

        return Ok(tickets.Select(ToDto));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var ticket = await _ticketService.GetTicketByIdAsync(id);
        if (ticket == null)
            return NotFound();


        return Ok(ToDetailDto(ticket));
    }

    [HttpGet("status-options")]
    public IActionResult GetStatusOptions()
    {
        var options = Enum.GetValues<ServiceTicketStatus>()
            .Select(status => new StatusOptionDto
            {
                Value = (int)status,
                Text = GetStatusText(status)
            });

        return Ok(options);
    }

    [HttpGet("next-number")]
    public async Task<IActionResult> GetNextTicketNumber()
    {
        var tickets = await _ticketService.GetAllTicketsAsync();
        var nextId = tickets.Count == 0 ? 1 : tickets.Max(t => t.Id) + 1;

        return Ok(new { ticketNumber = FormatTicketNumber(nextId) });
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CreateTicketRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var deviceName = request.DeviceName.Trim();
        if (string.IsNullOrWhiteSpace(deviceName))
            return BadRequest(new { message = "Cihaz adı boş olamaz." });

        var brand = request.Brand.Trim();
        if (string.IsNullOrWhiteSpace(brand))
            return BadRequest(new { message = "Marka boş olamaz." });

        if (request.CreatedDate > DateTime.Now)
            return BadRequest(new { message = "Servis fişi için ileri tarih veya saat seçilemez." });

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            Customer? customer;
            if (request.CustomerId is > 0)
            {
                customer = await _customerService.GetCustomerByIdAsync(request.CustomerId.Value);
                if (customer is null)
                    return BadRequest(new { message = "Seçilen müşteri bulunamadı." });
            }
            else
            {
                var newCustomer = request.NewCustomer;
                if (newCustomer is null ||
                    string.IsNullOrWhiteSpace(newCustomer.FirstName) ||
                    string.IsNullOrWhiteSpace(newCustomer.LastName) ||
                    string.IsNullOrWhiteSpace(newCustomer.PhoneNumber) ||
                    string.IsNullOrWhiteSpace(newCustomer.Email))
                {
                    return BadRequest(new { message = "Yeni müşteri için ad, soyad, telefon ve e-posta zorunludur." });
                }

                var serviceAccountId = GetServiceAccountId();
                if (serviceAccountId is null)
                    return Forbid();

                customer = await _customerService.CreateCustomerAsync(
                    newCustomer.FirstName.Trim(),
                    newCustomer.LastName.Trim(),
                    newCustomer.PhoneNumber.Trim(),
                    newCustomer.Email.Trim());
                customer.ServiceAccountId = serviceAccountId.Value;
                await _context.SaveChangesAsync();
            }

            var device = await _deviceService.CreateDeviceAsync(
                customer.Id,
                brand,
                deviceName,
                string.IsNullOrWhiteSpace(request.SerialNumber) ? null : request.SerialNumber.Trim(),
                "Servis fişi ile oluşturuldu");

            var ticketDate = request.CreatedDate == default ? DateTime.Now : request.CreatedDate;
            var ticket = await _ticketService.CreateTicketAsync(device.Id, ticketDate);
            ticket = await _ticketService.UpdateTicketDetailsAsync(
                ticket.Id,
                ticket.Status,
                request.Notes,
                request.EstimatedPrice);
            var trackingLink = await _trackingService.CreateOrGetTrackingLinkAsync(ticket.Id);

            ticket.Device = device;
            ticket.Device.Customer = customer;
            ticket.TrackingLink = trackingLink;

            await transaction.CommitAsync();

            var trackingUrl = BuildTrackingUrl(trackingLink.Token);
            var delivery = await TrySendTrackingEmailAsync(ticket, customer, trackingUrl);
            var response = ToDetailDto(ticket);
            response.EmailSent = delivery.Sent;
            response.EmailMessage = delivery.Message;

            return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Servis fişi oluşturulurken hata oluştu.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Servis fişi oluşturulamadı." });
        }
    }
    [HttpPut("{id}")]
    public async Task<IActionResult> Put(int id, [FromBody] UpdateTicketRequestDto request)
    {
        if (!Enum.IsDefined(typeof(ServiceTicketStatus), request.Status))
            return BadRequest("Geçersiz servis durumu.");

        var existingTicket = await _ticketService.GetTicketByIdAsync(id);
        if (existingTicket == null)
            return NotFound();


        try
        {
            var ticket = await _ticketService.UpdateTicketDetailsAsync(
                id,
                (ServiceTicketStatus)request.Status,
                request.Notes,
                request.EstimatedPrice);

            return Ok(ToDetailDto(ticket));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id}/send-email")]
    public async Task<IActionResult> SendEmail(int id)
    {
        var ticket = await _ticketService.GetTicketByIdAsync(id);
        if (ticket == null)
            return NotFound();

        var customer = ticket.Device?.Customer;
        if (customer is null || string.IsNullOrWhiteSpace(customer.Email))
            return BadRequest(new { message = "Müşterinin kayıtlı e-posta adresi bulunmuyor." });

        var trackingLink = await _trackingService.CreateOrGetTrackingLinkAsync(id);
        var trackingUrl = BuildTrackingUrl(trackingLink.Token);
        var delivery = await TrySendTrackingEmailAsync(ticket, customer, trackingUrl);
        if (!delivery.Sent)
            return StatusCode(StatusCodes.Status502BadGateway, new { message = delivery.Message });

        return Ok(new EmailSendResponseDto
        {
            ServiceTicketId = ticket.Id,
            TicketNumber = FormatTicketNumber(ticket.Id),
            CustomerName = $"{customer.FirstName} {customer.LastName}".Trim(),
            EmailAddress = MaskEmail(customer.Email),
            TrackingToken = trackingLink.Token,
            TrackingUrl = trackingUrl,
            Message = delivery.Message
        });
    }
    private static ServiceTicketDto ToDto(ServiceTicket ticket)
    {
        return new ServiceTicketDto
        {
            Id = ticket.Id,
            TicketNumber = FormatTicketNumber(ticket.Id),
            CustomerName = ticket.Device?.Customer == null
                ? string.Empty
                : $"{ticket.Device.Customer.FirstName} {ticket.Device.Customer.LastName}".Trim(),
            DeviceModel = ticket.Device?.Model ?? string.Empty,
            CreatedDate = ticket.CreatedAt,
            StatusValue = (int)ticket.Status,
            Status = GetStatusText(ticket.Status)
        };
    }

    private ServiceTicketDetailDto ToDetailDto(ServiceTicket ticket)
    {
        return new ServiceTicketDetailDto
        {
            Id = ticket.Id,
            TicketNumber = FormatTicketNumber(ticket.Id),
            CustomerName = ticket.Device?.Customer == null
                ? string.Empty
                : $"{ticket.Device.Customer.FirstName} {ticket.Device.Customer.LastName}".Trim(),
            DeviceModel = ticket.Device?.Model ?? string.Empty,
            CreatedDate = ticket.CreatedAt,
            StatusValue = (int)ticket.Status,
            Status = GetStatusText(ticket.Status),
            Brand = ticket.Device?.Brand ?? string.Empty,
            SerialNumber = ticket.Device?.SerialNumber,
            EstimatedPrice = ticket.EstimatedPrice,
            Notes = ticket.Notes,
            TrackingUrl = ticket.TrackingLink == null ? null : BuildTrackingUrl(ticket.TrackingLink.Token),
            StatusHistories = ticket.StatusHistories
                .OrderBy(history => history.ChangedAt)
                .Select(history => new StatusHistoryDto
                {
                    ChangedAt = history.ChangedAt,
                    Status = (int)history.Status,
                    StatusText = GetStatusText(history.Status),
                    Notes = history.Notes
                })
                .ToList()
        };
    }

    private string BuildTrackingUrl(string token)
    {
        var publicAppBaseUrl = _configuration["Email:PublicAppBaseUrl"];
        if (!string.IsNullOrWhiteSpace(publicAppBaseUrl))
            return $"{publicAppBaseUrl.TrimEnd('/')}/takip/{Uri.EscapeDataString(token)}";

        return $"{Request.Scheme}://{Request.Host}/api/Tracking/{Uri.EscapeDataString(token)}";
    }

    private async Task<EmailDeliveryResult> TrySendTrackingEmailAsync(
        ServiceTicket ticket,
        Customer customer,
        string trackingUrl)
    {
        if (string.IsNullOrWhiteSpace(customer.Email))
            return new(false, "Müşterinin kayıtlı e-posta adresi bulunmuyor.");

        var customerName = $"{customer.FirstName} {customer.LastName}".Trim();
        try
        {
            await _emailSender.SendServiceTicketAsync(new ServiceTicketEmail(
                customer.Email,
                customerName,
                FormatTicketNumber(ticket.Id),
                $"{ticket.Device.Brand} {ticket.Device.Model}".Trim(),
                trackingUrl));

            _logger.LogInformation(
                "Servis takip e-postası {MaskedEmail} adresine gönderildi. Servis fişi: {TicketNumber}",
                MaskEmail(customer.Email),
                FormatTicketNumber(ticket.Id));
            return new(true, $"E-posta gönderildi: {MaskEmail(customer.Email)}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Servis takip e-postası gönderilemedi. Servis fişi: {TicketNumber}", FormatTicketNumber(ticket.Id));
            return new(false, "Servis fişi kaydedildi ancak e-posta gönderilemedi. SMTP yapılandırmasını kontrol edin.");
        }
    }

    private static string MaskEmail(string email)
    {
        var separator = email.IndexOf('@');
        if (separator <= 1)
            return "***";

        return $"{email[0]}{new string('*', Math.Min(5, separator - 1))}{email[separator..]}";
    }

    private sealed record EmailDeliveryResult(bool Sent, string Message);
    private static string FormatTicketNumber(int id)
    {
        return $"SF-{id:000000}";
    }

    private int? GetServiceAccountId()
    {
        var idText = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idText, out var id) && id > 0 ? id : null;
    }

    private bool TicketBelongsToCurrentService(ServiceTicket ticket)
    {
        var serviceAccountId = GetServiceAccountId();
        return serviceAccountId != null && ticket.Device?.Customer?.ServiceAccountId == serviceAccountId.Value;
    }

    private static string MaskPhoneNumber(string phoneNumber)
    {
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (digits.Length < 4)
            return "****";

        var visiblePrefixLength = Math.Min(3, digits.Length);
        var visibleSuffix = digits[^4..];
        var hiddenLength = Math.Max(0, digits.Length - visiblePrefixLength - 4);

        return $"{digits[..visiblePrefixLength]}{new string('*', hiddenLength)}{visibleSuffix}";
    }

    private static string GetStatusText(ServiceTicketStatus status)
    {
        return (int)status switch
        {
            0 => "Teslim Alındı",
            1 => "İncelemede",
            2 => "Parça Bekleniyor",
            3 => "Tamir Ediliyor",
            4 => "Test Ediliyor",
            5 => "Hazır",
            6 => "Teslim Edildi",
            7 => "İptal",
            _ => status.ToString()
        };
    }
}
