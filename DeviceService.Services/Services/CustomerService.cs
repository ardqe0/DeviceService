using DeviceService.Core.Entities;
using DeviceService.Core.Interfaces;

namespace DeviceService.Services;

public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customerRepository;

    public CustomerService(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<Customer?> GetCustomerByIdAsync(int id)
    {
        return await _customerRepository.GetByIdAsync(id);
    }

    public async Task<List<Customer>> GetAllCustomersAsync()
    {
        var customers = await _customerRepository.GetAllAsync();
        return customers.ToList();
    }

    public async Task<Customer> CreateCustomerAsync(string firstName, string lastName, string phoneNumber, string? email)
    {
        var customer = new Customer
        {
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
            Email = email,
            CreatedAt = DateTime.Now
        };

        await _customerRepository.AddAsync(customer);
        await _customerRepository.SaveChangesAsync();  // ← BU SATIRI EKLE
        return customer;
    }

    public async Task UpdateCustomerAsync(int id, string firstName, string lastName, string phoneNumber, string? email)
    {
        var customer = await _customerRepository.GetByIdAsync(id);
        if (customer == null)
            throw new KeyNotFoundException($"Customer with ID {id} not found");

        customer.FirstName = firstName;
        customer.LastName = lastName;
        customer.PhoneNumber = phoneNumber;
        customer.Email = email;

        _customerRepository.Update(customer);
        await _customerRepository.SaveChangesAsync();
    }

    public async Task DeleteCustomerAsync(int id)
    {
        var customer = await _customerRepository.GetByIdAsync(id);
        if (customer == null)
            throw new KeyNotFoundException($"Customer with ID {id} not found");

        _customerRepository.Delete(customer);
        await _customerRepository.SaveChangesAsync();
    }
}