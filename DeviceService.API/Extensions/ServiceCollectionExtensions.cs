using DeviceService.Core.Interfaces;
using DeviceService.Data;
using DeviceService.Data.Repositories;
using DeviceService.Services;
using Microsoft.EntityFrameworkCore;

namespace DeviceService.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDeviceServiceInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DeviceServiceDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IServiceTicketRepository, ServiceTicketRepository>();
        services.AddScoped<IStatusHistoryRepository, StatusHistoryRepository>();
        services.AddScoped<ITrackingLinkRepository, TrackingLinkRepository>();

        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IDeviceService, DeviceService.Services.DeviceService>();
        services.AddScoped<IServiceTicketService, ServiceTicketService>();
        services.AddScoped<ITrackingService, TrackingService>();

        return services;
    }
}
