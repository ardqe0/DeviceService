using DeviceService.Core.Interfaces;
using DeviceService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace DeviceService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Service")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly DeviceServiceDbContext _context;

    public CustomersController(ICustomerService customerService, DeviceServiceDbContext context)
    {
        _customerService = customerService;
        _context = context;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCustomer(int id)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(customer => customer.Id == id);

        if (customer == null)
            return NotFound();

        return Ok(customer);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllCustomers()
    {
        var customers = await _context.Customers
            .OrderByDescending(customer => customer.Id)

            .ToListAsync();
        return Ok(customers);
    }

    [HttpPost]

    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var serviceAccountId = GetServiceAccountId();
        if (serviceAccountId == null)
            return Forbid();

        var customer = await _customerService.CreateCustomerAsync(
            request.FirstName,
            request.LastName,
            request.PhoneNumber,
            request.Email);

        customer.ServiceAccountId = serviceAccountId.Value;
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, customer);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCustomer(int id, [FromBody] UpdateCustomerRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var serviceAccountId = GetServiceAccountId();
        if (serviceAccountId == null)
            return Forbid();

        if (!await CustomerBelongsToServiceAsync(id, serviceAccountId.Value))
            return NotFound();

        try
        {
            await _customerService.UpdateCustomerAsync(id,
                request.FirstName,
                request.LastName,
                request.PhoneNumber,
                request.Email);

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCustomer(int id)
    {
        var serviceAccountId = GetServiceAccountId();
        if (serviceAccountId == null)
            return Forbid();

        if (!await CustomerBelongsToServiceAsync(id, serviceAccountId.Value))
            return NotFound();

        try
        {
            await _customerService.DeleteCustomerAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private int? GetServiceAccountId()
    {
        var idText = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idText, out var id) && id > 0 ? id : null;
    }

    private async Task<bool> CustomerBelongsToServiceAsync(int customerId, int serviceAccountId)
    {
        return await _context.Customers.AnyAsync(customer =>
            customer.Id == customerId && customer.ServiceAccountId == serviceAccountId);
    }
}

public class CreateCustomerRequest
{
    [JsonPropertyName("FirstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("LastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("PhoneNumber")]
    public string PhoneNumber { get; set; } = string.Empty;

    [JsonPropertyName("Email")]
    public string Email { get; set; } = string.Empty;
}

public class UpdateCustomerRequest
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string? Email { get; set; }
}
