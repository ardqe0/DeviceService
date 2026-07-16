using DeviceService.Core.Entities;
using DeviceService.Core.Enums;

namespace DeviceService.Core.Interfaces;

public interface IServiceTicketService
{
    Task<ServiceTicket?> GetTicketByIdAsync(int id);
    Task<List<ServiceTicket>> GetAllTicketsAsync();
    Task<List<ServiceTicket>> GetTicketsByStatusAsync(ServiceTicketStatus status);
    Task<List<ServiceTicket>> GetTicketsByDeviceAsync(int deviceId);
    Task<ServiceTicket> CreateTicketAsync(int deviceId);
    Task<ServiceTicket> CreateTicketAsync(int deviceId, DateTime createdAt);
    Task<ServiceTicket> UpdateTicketDetailsAsync(int id, ServiceTicketStatus newStatus, string? notes, decimal? estimatedPrice);
    Task UpdateTicketStatusAsync(int id, ServiceTicketStatus newStatus, string? notes);
    Task UpdateTicketNotesAsync(int id, string notes);
    Task UpdateTicketEstimatedPriceAsync(int id, decimal price);
    Task<List<StatusHistory>> GetTicketHistoryAsync(int id);
}
