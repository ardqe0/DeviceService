using DeviceService.API.DTOs;
using DeviceService.Core.Entities;
using DeviceService.Core.Enums;
using DeviceService.Core.Interfaces;
using DeviceService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeviceService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class TrackingController : ControllerBase
{
    private readonly ITrackingService _trackingService;
    private readonly IServiceTicketService _ticketService;
    private readonly DeviceServiceDbContext _context;
    private const int MaxVerificationAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(10);

    public TrackingController(
        ITrackingService trackingService,
        IServiceTicketService ticketService,
        DeviceServiceDbContext context)
    {
        _trackingService = trackingService;
        _ticketService = ticketService;
        _context = context;
    }

    [HttpGet("{token}")]
    public async Task<IActionResult> StartVerification(string token)
    {
        var ticket = await GetTicketForTokenAsync(token);
        if (ticket is null)
            return NotFound(new { message = "Takip kayd\u0131 bulunamad\u0131." });

        var attempt = await GetAttemptAsync(token);
        if (attempt.LockedUntil is not null && attempt.LockedUntil > DateTimeOffset.UtcNow)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                message = "\u00c7ok fazla hatal\u0131 deneme yap\u0131ld\u0131. L\u00fctfen 10 dakika sonra tekrar deneyin.",
                attemptsRemaining = 0
            });
        }

        return Ok(new TrackingChallengeResponseDto
        {
            MaskedPhoneNumber = MaskPhoneNumber(ticket.Device.Customer.PhoneNumber),
            AttemptsRemaining = Math.Max(0, MaxVerificationAttempts - attempt.FailedAttempts)
        });
    }

    [HttpPost("verify")]
    public async Task<IActionResult> VerifyPhone([FromBody] TrackingVerificationRequestDto request)
    {
        var token = request.Token.Trim();
        var phoneLastFour = DigitsOnly(request.PhoneLastFour);
        if (string.IsNullOrWhiteSpace(token) || phoneLastFour.Length != 4)
        {
            return BadRequest(new { message = "Telefon numaras\u0131n\u0131n son 4 hanesini girin." });
        }

        var ticket = await GetTicketForTokenAsync(token);
        if (ticket is null)
            return NotFound(new { message = "Takip kayd\u0131 bulunamad\u0131." });

        var attempt = await GetAttemptAsync(token);
        if (attempt.LockedUntil is not null && attempt.LockedUntil > DateTimeOffset.UtcNow)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                message = "\u00c7ok fazla hatal\u0131 deneme yap\u0131ld\u0131. L\u00fctfen 10 dakika sonra tekrar deneyin.",
                attemptsRemaining = 0
            });
        }

        var phoneNumber = DigitsOnly(ticket.Device.Customer.PhoneNumber);
        if (!phoneNumber.EndsWith(phoneLastFour, StringComparison.Ordinal))
        {
            attempt.FailedAttempts++;
            var remaining = Math.Max(0, MaxVerificationAttempts - attempt.FailedAttempts);
            if (remaining == 0)
                attempt.LockedUntil = DateTimeOffset.UtcNow.Add(LockoutDuration);

            attempt.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message = remaining == 0
                    ? "Deneme s\u0131n\u0131r\u0131na ula\u015f\u0131ld\u0131. L\u00fctfen 10 dakika sonra tekrar deneyin."
                    : "Telefon do\u011frulamas\u0131 hatal\u0131.",
                attemptsRemaining = remaining
            });
        }

        _context.TrackingVerificationAttempts.Remove(attempt);
        await _context.SaveChangesAsync();

        return Ok(CreateTrackingResponse(ticket));
    }

    private async Task<DeviceService.Core.Entities.ServiceTicket?> GetTicketForTokenAsync(string token)
    {
        var trackingLink = await _trackingService.GetTrackingLinkByTokenAsync(token);
        if (trackingLink == null || (trackingLink.ExpiresAt.HasValue && trackingLink.ExpiresAt < DateTime.Now))
            return null;

        return await _ticketService.GetTicketByIdAsync(trackingLink.ServiceTicketId);
    }

    private static TrackingResponseDto CreateTrackingResponse(DeviceService.Core.Entities.ServiceTicket ticket)
    {
        return new TrackingResponseDto
        {
            DeviceInfo = new TrackingDeviceDto
            {
                Brand = ticket.Device.Brand,
                Model = ticket.Device.Model,
                SerialNumber = ticket.Device.SerialNumber,
                Complaint = ticket.Device.ComplaintDescription
            },
            CurrentStatus = GetStatusText(ticket.Status),
            EstimatedPrice = ticket.EstimatedPrice,
            StatusHistory = ticket.StatusHistories
                .OrderBy(history => history.ChangedAt)
                .Select(history => new TrackingHistoryDto
                {
                    Status = GetStatusText(history.Status),
                    ChangedAt = history.ChangedAt,
                    Notes = history.Notes
                })
                .ToList()
        };

    }

    private async Task<TrackingVerificationAttempt> GetAttemptAsync(string token)
    {
        var remoteAddress = GetRemoteAddress();
        var attempt = await _context.TrackingVerificationAttempts
            .SingleOrDefaultAsync(attempt => attempt.Token == token && attempt.RemoteAddress == remoteAddress);

        if (attempt != null)
            return attempt;

        attempt = new TrackingVerificationAttempt
        {
            Token = token,
            RemoteAddress = remoteAddress
        };

        _context.TrackingVerificationAttempts.Add(attempt);
        return attempt;
    }

    private string GetRemoteAddress() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static string MaskPhoneNumber(string phoneNumber)
    {
        var digits = DigitsOnly(phoneNumber);
        if (digits.Length == 11 && digits[0] == '0')
            digits = digits[1..];

        if (digits.Length == 10)
            return $"{digits[..3]} {digits[3]}** ****";

        return digits.Length <= 4
            ? new string('*', digits.Length)
            : $"{digits[..4]} {new string('*', digits.Length - 4)}";
    }

    private static string DigitsOnly(string value) => new(value.Where(char.IsDigit).ToArray());

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
