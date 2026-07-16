using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DeviceService.Data;

public class DeviceServiceDbContextFactory : IDesignTimeDbContextFactory<DeviceServiceDbContext>
{
    public DeviceServiceDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DeviceServiceDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=DeviceServiceDb;Trusted_Connection=true;");

        return new DeviceServiceDbContext(optionsBuilder.Options);
    }
}
