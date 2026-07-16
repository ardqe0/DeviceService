using DeviceService.Core.Entities;
using DeviceService.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DeviceService.Data.Repositories;

public class CustomerRepository : BaseRepository<Customer>, ICustomerRepository
{
    public CustomerRepository(DeviceServiceDbContext context) : base(context)
    {
    }

    public async Task<Customer?> GetCustomerWithDevicesAsync(int id)
    {
        return await _dbSet.Include(c => c.Devices).FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<List<Customer>> GetAllWithDevicesAsync()
    {
        return await _dbSet.Include(c => c.Devices).ToListAsync();
    }
}
