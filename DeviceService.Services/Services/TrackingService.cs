using DeviceService.Core.Entities;
using DeviceService.Core.Interfaces;
using System.Security.Cryptography;

namespace DeviceService.Services;

public class TrackingService : ITrackingService
{
    private readonly ITrackingLinkRepository _trackingLinkRepository;
    private readonly IServiceTicketRepository _ticketRepository;

    public TrackingService(ITrackingLinkRepository trackingLinkRepository, IServiceTicketRepository ticketRepository)
    {
        _trackingLinkRepository = trackingLinkRepository;
        _ticketRepository = ticketRepository;
    }

    public async Task<TrackingLink?> GetTrackingLinkByTokenAsync(string token)
    {
        return await _trackingLinkRepository.GetByTokenAsync(token);
    }

    public async Task<TrackingLink> CreateOrGetTrackingLinkAsync(int serviceTicketId)
    {
        var existingLink = await _trackingLinkRepository.GetByServiceTicketIdAsync(serviceTicketId);
        if (existingLink != null)
            return existingLink;

        var token = await GenerateTokenAsync(serviceTicketId);
        var trackingLink = new TrackingLink
        {
            ServiceTicketId = serviceTicketId,
            Token = token,
            CreatedAt = DateTime.Now,
            ExpiresAt = DateTime.Now.AddDays(30)
        };

        await _trackingLinkRepository.AddAsync(trackingLink);
        return trackingLink;
    }

    public async Task<string> GenerateTrackingLinkAsync(int serviceTicketId)
    {
        var trackingLink = await CreateOrGetTrackingLinkAsync(serviceTicketId);
        return trackingLink.Token;
    }

    private async Task<string> GenerateTokenAsync(int serviceTicketId)
    {
        string token;
        TrackingLink? existingLink;

        do
        {
            token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
            existingLink = await _trackingLinkRepository.GetByTokenAsync(token);
        }
        while (existingLink != null);

        return token;
    }
}
