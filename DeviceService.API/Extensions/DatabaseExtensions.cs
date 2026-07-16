using DeviceService.Data;
using Microsoft.EntityFrameworkCore;

namespace DeviceService.API.Extensions;

public static class DatabaseExtensions
{
    public static void ApplyMigrations(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DeviceServiceDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigration");

        const int maxAttempts = 10;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                context.Database.Migrate();
                logger.LogInformation("Database migrations applied successfully.");
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex, "Database migration attempt {Attempt}/{MaxAttempts} failed. Retrying.", attempt, maxAttempts);
                Thread.Sleep(TimeSpan.FromSeconds(3));
            }
        }

        context.Database.Migrate();
    }
}