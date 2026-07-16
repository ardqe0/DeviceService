using DeviceService.Core.Entities;
using DeviceService.Core.Enums;
using DeviceService.Core.Interfaces;

namespace DeviceService.Services;

public class ServiceTicketService : IServiceTicketService
{
    private readonly IServiceTicketRepository _ticketRepository;
    private readonly IStatusHistoryRepository _historyRepository;

    public ServiceTicketService(IServiceTicketRepository ticketRepository, IStatusHistoryRepository historyRepository)
    {
        _ticketRepository = ticketRepository;
        _historyRepository = historyRepository;
    }

    public async Task<ServiceTicket?> GetTicketByIdAsync(int id)
    {
        return await _ticketRepository.GetTicketWithHistoryAsync(id);
    }

    public async Task<List<ServiceTicket>> GetAllTicketsAsync()
    {
        return await _ticketRepository.GetAllTicketsAsync();
    }

    public async Task<List<ServiceTicket>> GetTicketsByStatusAsync(ServiceTicketStatus status)
    {
        return await _ticketRepository.GetTicketsByStatusAsync(status);
    }

    public async Task<List<ServiceTicket>> GetTicketsByDeviceAsync(int deviceId)
    {
        return await _ticketRepository.GetTicketsByDeviceAsync(deviceId);
    }

    public async Task<ServiceTicket> CreateTicketAsync(int deviceId)
    {
        return await CreateTicketAsync(deviceId, DateTime.Now);
    }

    public async Task<ServiceTicket> CreateTicketAsync(int deviceId, DateTime createdAt)
    {
        var ticket = new ServiceTicket
        {
            DeviceId = deviceId,
            Status = ServiceTicketStatus.TeslimAlindi,
            CreatedAt = createdAt
        };

        await _ticketRepository.AddAsync(ticket);

        var history = new StatusHistory
        {
            ServiceTicketId = ticket.Id,
            Status = ServiceTicketStatus.TeslimAlindi,
            ChangedAt = createdAt,
            Notes = "Cihaz teslim alındı"
        };

        await _historyRepository.AddAsync(history);

        return ticket;
    }

    public async Task<ServiceTicket> UpdateTicketDetailsAsync(int id, ServiceTicketStatus newStatus, string? notes, decimal? estimatedPrice)
    {
        var ticket = await _ticketRepository.GetTicketWithHistoryAsync(id);
        if (ticket == null)
            throw new KeyNotFoundException($"Service ticket with ID {id} not found");

        var statusChanged = ticket.Status != newStatus;

        ticket.Status = newStatus;
        ticket.Notes = notes;
        ticket.EstimatedPrice = estimatedPrice;
        ticket.UpdatedAt = DateTime.Now;

        _ticketRepository.Update(ticket);
        await _ticketRepository.SaveChangesAsync();

        if (statusChanged)
        {
            var history = new StatusHistory
            {
                ServiceTicketId = id,
                Status = newStatus,
                ChangedAt = DateTime.Now,
                Notes = notes
            };

            await _historyRepository.AddAsync(history);
            ticket.StatusHistories.Add(history);
        }

        return ticket;
    }

    public async Task UpdateTicketStatusAsync(int id, ServiceTicketStatus newStatus, string? notes)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id);
        if (ticket == null)
            throw new KeyNotFoundException($"Service ticket with ID {id} not found");

        ticket.Status = newStatus;
        ticket.UpdatedAt = DateTime.Now;

        _ticketRepository.Update(ticket);
        await _ticketRepository.SaveChangesAsync();

        var history = new StatusHistory
        {
            ServiceTicketId = id,
            Status = newStatus,
            ChangedAt = DateTime.Now,
            Notes = notes
        };

        await _historyRepository.AddAsync(history);
    }

    public async Task UpdateTicketNotesAsync(int id, string notes)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id);
        if (ticket == null)
            throw new KeyNotFoundException($"Service ticket with ID {id} not found");

        ticket.Notes = notes;
        ticket.UpdatedAt = DateTime.Now;

        _ticketRepository.Update(ticket);
        await _ticketRepository.SaveChangesAsync();
    }

    public async Task UpdateTicketEstimatedPriceAsync(int id, decimal price)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id);
        if (ticket == null)
            throw new KeyNotFoundException($"Service ticket with ID {id} not found");

        ticket.EstimatedPrice = price;
        ticket.UpdatedAt = DateTime.Now;

        _ticketRepository.Update(ticket);
        await _ticketRepository.SaveChangesAsync();
    }

    public async Task<List<StatusHistory>> GetTicketHistoryAsync(int id)
    {
        return await _historyRepository.GetHistoryByTicketAsync(id);
    }
}
