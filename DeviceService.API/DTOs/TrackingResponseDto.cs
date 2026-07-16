namespace DeviceService.API.DTOs;

public class TrackingResponseDto
{
    public TrackingDeviceDto DeviceInfo { get; set; } = new();
    public string CurrentStatus { get; set; } = string.Empty;
    public decimal? EstimatedPrice { get; set; }
    public List<TrackingHistoryDto> StatusHistory { get; set; } = new();
}

public class TrackingChallengeResponseDto
{
    public string MaskedPhoneNumber { get; set; } = string.Empty;
    public int AttemptsRemaining { get; set; }
}

public class TrackingVerificationRequestDto
{
    public string Token { get; set; } = string.Empty;
    public string PhoneLastFour { get; set; } = string.Empty;
}

public class TrackingDeviceDto
{
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string Complaint { get; set; } = string.Empty;
}

public class TrackingHistoryDto
{
    public string Status { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string? Notes { get; set; }
}
