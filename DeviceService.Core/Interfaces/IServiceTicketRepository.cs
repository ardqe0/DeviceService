using DeviceService.Core.Entities;
using DeviceService.Core.Enums;


namespace DeviceService.Core.Interfaces;

public interface IServiceTicketRepository : IRepository<ServiceTicket>
{
    Task<ServiceTicket?> GetTicketWithHistoryAsync(int id);
    Task<List<ServiceTicket>> GetAllTicketsAsync();
    Task<List<ServiceTicket>> GetTicketsByStatusAsync(ServiceTicketStatus status);
    Task<List<ServiceTicket>> GetTicketsByDeviceAsync(int deviceId);
}
