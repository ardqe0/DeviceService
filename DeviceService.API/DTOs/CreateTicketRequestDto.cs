using System.ComponentModel.DataAnnotations;
namespace DeviceService.API.DTOs;

public class CreateTicketRequestDto
{
    public int? CustomerId { get; set; }

    public NewTicketCustomerDto? NewCustomer { get; set; }

    [Required]
    public string DeviceName { get; set; } = string.Empty;

    [Required]
    public string Brand { get; set; } = string.Empty;

    public string? SerialNumber { get; set; }
    public decimal? EstimatedPrice { get; set; }
    public string? Notes { get; set; }

    [Required]
    public DateTime CreatedDate { get; set; }
}


public class NewTicketCustomerDto
{
    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    [Required]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
public class ServiceTicketDto
{
    public int Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public int StatusValue { get; set; }
    public string Status { get; set; } = string.Empty;
}
