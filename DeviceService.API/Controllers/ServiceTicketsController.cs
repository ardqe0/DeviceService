using DeviceService.API.DTOs;
using DeviceService.API.Services;
using DeviceService.Data;
using DeviceService.Core.Entities;
using DeviceService.Core.Enums;
using DeviceService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

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
    private readonly IWebHostEnvironment _environment;

    public ServiceTicketsController(
        IServiceTicketService ticketService,
        IDeviceService deviceService,
        ICustomerService customerService,
        ITrackingService trackingService,
        IEmailSender emailSender,
        DeviceServiceDbContext context,
        IConfiguration configuration,
        ILogger<ServiceTicketsController> logger,
        IWebHostEnvironment environment)
    {
        _ticketService = ticketService;
        _deviceService = deviceService;
        _customerService = customerService;
        _trackingService = trackingService;
        _emailSender = emailSender;
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
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


        if ((ServiceTicketStatus)request.Status == ServiceTicketStatus.TeslimEdildi &&
            (string.IsNullOrWhiteSpace(existingTicket.DeliveryDevicePhotoPath) ||
             string.IsNullOrWhiteSpace(existingTicket.DeliveryDeviceBackPhotoPath) ||
             string.IsNullOrWhiteSpace(existingTicket.DeliveryIdentityDocumentPhotoPath) ||
             string.IsNullOrWhiteSpace(existingTicket.DeliveryIdentityDocumentBackPhotoPath) ||
             string.IsNullOrWhiteSpace(existingTicket.DeliveryRecipientFullName)))
        {
            return BadRequest(new { message = "Teslim işlemi için teslim alan kişi, cihaz fotoğrafı ve kimlik belgesinin ön ve arka yüz fotoğrafları zorunludur." });
        }

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
    [HttpPost("{id}/delivery-evidence")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> SaveDeliveryEvidence(int id, [FromForm] DeliveryEvidenceRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RecipientFullName))
            return BadRequest(new { message = "Teslim alan kişinin adı soyadı zorunludur." });

        var validationError = await ValidateDeliveryImagesAsync(request.DeviceFrontPhoto, request.DeviceBackPhoto, request.IdentityDocumentFrontPhoto, request.IdentityDocumentBackPhoto, cancellationToken);
        if (validationError is not null)
            return BadRequest(new { message = validationError });

        var ticket = await _ticketService.GetTicketByIdAsync(id);
        if (ticket is null)
            return NotFound();

        string? devicePhotoPath = null;
        string? deviceBackPhotoPath = null;
        string? identityDocumentPhotoPath = null;
        string? identityDocumentBackPhotoPath = null;
        try
        {
            devicePhotoPath = await SaveDeliveryImageAsync(request.DeviceFrontPhoto!, "cihaz-on", cancellationToken);
            deviceBackPhotoPath = await SaveDeliveryImageAsync(request.DeviceBackPhoto!, "cihaz-arka", cancellationToken);
            identityDocumentPhotoPath = await SaveDeliveryImageAsync(request.IdentityDocumentFrontPhoto!, "kimlik-on", cancellationToken);
            identityDocumentBackPhotoPath = await SaveDeliveryImageAsync(request.IdentityDocumentBackPhoto!, "kimlik-arka", cancellationToken);

            ticket.DeliveryRecipientFullName = request.RecipientFullName.Trim();
            ticket.DeliveryDevicePhotoPath = devicePhotoPath;
            ticket.DeliveryDeviceBackPhotoPath = deviceBackPhotoPath;
            ticket.DeliveryIdentityDocumentPhotoPath = identityDocumentPhotoPath;
            ticket.DeliveryIdentityDocumentBackPhotoPath = identityDocumentBackPhotoPath;
            ticket.DeliveredAt = DateTime.Now;

            var updatedTicket = await _ticketService.UpdateTicketDetailsAsync(
                id,
                ServiceTicketStatus.TeslimEdildi,
                request.Notes,
                request.EstimatedPrice);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Teslim kanıtları kaydedildi. Servis fişi: {TicketNumber}", FormatTicketNumber(id));
            return Ok(ToDetailDto(updatedTicket));
        }
        catch (Exception ex)
        {
            DeleteEvidenceFile(devicePhotoPath);
            DeleteEvidenceFile(deviceBackPhotoPath);
            DeleteEvidenceFile(identityDocumentPhotoPath);
            DeleteEvidenceFile(identityDocumentBackPhotoPath);
            _logger.LogError(ex, "Teslim kanıtları kaydedilemedi. Servis fişi: {TicketNumber}", FormatTicketNumber(id));
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Teslim kanıtları kaydedilemedi." });
        }
    }

    [HttpGet("{id}/pdf")]
    public async Task<IActionResult> DownloadPdf(int id)
    {
        var ticket = await _ticketService.GetTicketByIdAsync(id);
        if (ticket is null)
            return NotFound();

        var pdf = CreateServiceTicketPdf(ticket);
        return File(pdf, "application/pdf", $"{FormatTicketNumber(ticket.Id)}-servis-fisi.pdf");
    }

    private async Task<string?> ValidateDeliveryImagesAsync(
        IFormFile? deviceFrontPhoto,
        IFormFile? deviceBackPhoto,
        IFormFile? identityDocumentFrontPhoto,
        IFormFile? identityDocumentBackPhoto,
        CancellationToken cancellationToken)
    {
        var deviceError = await ValidateImageAsync(deviceFrontPhoto, "Cihaz ön yüz fotoğrafı", cancellationToken);
        if (deviceError is not null)
            return deviceError;

        var deviceBackError = await ValidateImageAsync(deviceBackPhoto, "Cihaz arka yüz fotoğrafı", cancellationToken);
        if (deviceBackError is not null)
            return deviceBackError;

        var identityFrontError = await ValidateImageAsync(identityDocumentFrontPhoto, "Kimlik belgesi ön yüz fotoğrafı", cancellationToken);
        if (identityFrontError is not null)
            return identityFrontError;

        return await ValidateImageAsync(identityDocumentBackPhoto, "Kimlik belgesi arka yüz fotoğrafı", cancellationToken);
    }

    private static async Task<string?> ValidateImageAsync(IFormFile? file, string fieldName, CancellationToken cancellationToken)
    {
        const long maximumFileSize = 5 * 1024 * 1024;
        if (file is null || file.Length == 0)
            return $"{fieldName} zorunludur.";
        if (file.Length > maximumFileSize)
            return $"{fieldName} en fazla 5 MB olabilir.";

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not ".jpg" and not ".jpeg" and not ".png" and not ".webp")
            return $"{fieldName} JPG, PNG veya WEBP biçiminde olmalıdır.";

        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        var isJpeg = read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var isPng = read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var isWebp = read >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8);

        return isJpeg || isPng || isWebp ? null : $"{fieldName} geçerli bir görsel dosyası değil.";
    }

    private async Task<string> SaveDeliveryImageAsync(IFormFile file, string label, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var relativePath = Path.Combine("App_Data", "DeliveryEvidence", $"{DateTime.UtcNow:yyyyMMddHHmmss}-{label}-{Guid.NewGuid():N}{extension}");
        var absolutePath = Path.Combine(_environment.ContentRootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using var output = System.IO.File.Create(absolutePath);
        await file.CopyToAsync(output, cancellationToken);
        return relativePath;
    }

    private void DeleteEvidenceFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return;

        var absolutePath = Path.Combine(_environment.ContentRootPath, relativePath);
        if (System.IO.File.Exists(absolutePath))
            System.IO.File.Delete(absolutePath);
    }

    private byte[]? ReadEvidencePhoto(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        var root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "App_Data", "DeliveryEvidence"));
        var fullPath = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, relativePath));
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !System.IO.File.Exists(fullPath))
            return null;

        return System.IO.File.ReadAllBytes(fullPath);
    }
    private byte[] CreateServiceTicketPdf(ServiceTicket ticket)
    {
        var customerName = ticket.Device?.Customer is null
            ? "-"
            : $"{ticket.Device.Customer.FirstName} {ticket.Device.Customer.LastName}".Trim();
        var deviceName = $"{ticket.Device?.Brand} {ticket.Device?.Model}".Trim();
        var statusHistory = ticket.StatusHistories.OrderBy(history => history.ChangedAt).ToList();
        var deviceFrontPhoto = ReadEvidencePhoto(ticket.DeliveryDevicePhotoPath);
        var deviceBackPhoto = ReadEvidencePhoto(ticket.DeliveryDeviceBackPhotoPath);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(style => style.FontSize(10));
                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item().Text("DeviceService").FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                        column.Item().Text("Servis Fişi").FontSize(12).FontColor(Colors.Grey.Darken1);
                    });
                    row.ConstantItem(150).AlignRight().Column(column =>
                    {
                        column.Item().Text(FormatTicketNumber(ticket.Id)).FontSize(16).Bold();
                        column.Item().Text($"Kayıt: {ticket.CreatedAt:dd.MM.yyyy HH:mm}").FontSize(9);
                    });
                });

                page.Content().PaddingVertical(20).Column(column =>
                {
                    column.Spacing(12);
                    column.Item().Element(Section).Text("Müşteri ve cihaz").Bold().FontSize(13);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns => { columns.RelativeColumn(); columns.RelativeColumn(); });
                        AddCell(table, "Müşteri", customerName);
                        AddCell(table, "Cihaz", deviceName);
                        AddCell(table, "Seri no", ticket.Device?.SerialNumber ?? "-");
                        AddCell(table, "Durum", GetStatusText(ticket.Status));
                        AddCell(table, "Tahmini ücret", ticket.EstimatedPrice?.ToString("C", new System.Globalization.CultureInfo("tr-TR")) ?? "-");
                        AddCell(table, "Teslim zamanı", ticket.DeliveredAt?.ToString("dd.MM.yyyy HH:mm") ?? "-");
                    });

                    if (deviceFrontPhoto is not null || deviceBackPhoto is not null)
                    {
                        column.Item().Element(Section).Text("Cihaz teslim fotoğrafları").Bold().FontSize(13);
                        column.Item().Row(row =>
                        {
                            if (deviceFrontPhoto is not null)
                            {
                                row.RelativeItem().Column(photoColumn =>
                                {
                                    photoColumn.Item().Text("Ön yüz").Bold();
                                    photoColumn.Item().PaddingTop(5).Height(180).Image(deviceFrontPhoto).FitArea();
                                });
                            }
                            if (deviceBackPhoto is not null)
                            {
                                row.RelativeItem().Column(photoColumn =>
                                {
                                    photoColumn.Item().Text("Arka yüz").Bold();
                                    photoColumn.Item().PaddingTop(5).Height(180).Image(deviceBackPhoto).FitArea();
                                });
                            }
                        });
                    }
                    if (!string.IsNullOrWhiteSpace(ticket.Notes))
                    {
                        column.Item().Element(Section).Text("Notlar").Bold().FontSize(13);
                        column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Text(ticket.Notes);
                    }

                    if (!string.IsNullOrWhiteSpace(ticket.DeliveryRecipientFullName))
                    {
                        column.Item().Element(Section).Text("Teslim bilgisi").Bold().FontSize(13);
                        column.Item().Text($"Teslim alan: {ticket.DeliveryRecipientFullName}");
                        column.Item().Text("Cihaz ve kimlik belgesi görselleri güvenli teslim kanıtı olarak sistemde saklanır; PDF'e eklenmez.").FontSize(9).FontColor(Colors.Grey.Darken1);
                    }

                    column.Item().Element(Section).Text("Durum geçmişi").Bold().FontSize(13);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns => { columns.ConstantColumn(110); columns.RelativeColumn(); columns.RelativeColumn(); });
                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Tarih");
                            header.Cell().Element(HeaderCell).Text("Durum");
                            header.Cell().Element(HeaderCell).Text("Not");
                        });
                        foreach (var history in statusHistory)
                        {
                            table.Cell().Element(BodyCell).Text(history.ChangedAt.ToString("dd.MM.yyyy HH:mm"));
                            table.Cell().Element(BodyCell).Text(GetStatusText(history.Status));
                            table.Cell().Element(BodyCell).Text(history.Notes ?? "-");
                        }
                    });
                });

                page.Footer().AlignCenter().DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Darken1)).Text(text =>
                {
                    text.Span("DeviceService - Servis fişi ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();

        static IContainer Section(IContainer container) => container.PaddingTop(4);
        static IContainer HeaderCell(IContainer container) => container.Background(Colors.Blue.Lighten5).Padding(6).BorderBottom(1).BorderColor(Colors.Blue.Lighten2);
        static IContainer BodyCell(IContainer container) => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(6).PaddingHorizontal(4);
        static void AddCell(TableDescriptor table, string label, string value)
        {
            table.Cell().Element(BodyCell).Column(column =>
            {
                column.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
                column.Item().Text(value).Bold();
            });
        }
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
            DeliveryRecipientFullName = ticket.DeliveryRecipientFullName,
            DeliveredAt = ticket.DeliveredAt,
            HasDeliveryEvidence = !string.IsNullOrWhiteSpace(ticket.DeliveryDevicePhotoPath) &&
                                  !string.IsNullOrWhiteSpace(ticket.DeliveryDeviceBackPhotoPath) &&
                                  !string.IsNullOrWhiteSpace(ticket.DeliveryIdentityDocumentPhotoPath) &&
                                  !string.IsNullOrWhiteSpace(ticket.DeliveryIdentityDocumentBackPhotoPath),
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
