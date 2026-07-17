namespace DeviceService.API.DTOs;

public class UpdateTicketRequestDto
{
    public int Status { get; set; }
    public decimal? EstimatedPrice { get; set; }
    public string? Notes { get; set; }
}

public class ServiceTicketDetailDto : ServiceTicketDto
{
    public string Brand { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public decimal? EstimatedPrice { get; set; }
    public string? Notes { get; set; }
    public string? TrackingUrl { get; set; }
    public string? DeliveryRecipientFullName { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public bool HasDeliveryEvidence { get; set; }
    public bool EmailSent { get; set; }
    public string? EmailMessage { get; set; }
    public List<StatusHistoryDto>
 StatusHistories { get; set; } = new();
}

public class DeliveryEvidenceRequestDto
{
    public string RecipientFullName { get; set; } = string.Empty;
    public decimal? EstimatedPrice { get; set; }
    public string? Notes { get; set; }
    public IFormFile? DevicePhoto { get; set; }
    public IFormFile? IdentityDocumentPhoto { get; set; }
}

public class StatusHistoryDto
{
    public DateTime ChangedAt { get; set; }
    public int Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class StatusOptionDto
{
    public int Value { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class EmailSendResponseDto
{
    public int ServiceTicketId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string TrackingToken { get; set; } = string.Empty;
    public string TrackingUrl { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
