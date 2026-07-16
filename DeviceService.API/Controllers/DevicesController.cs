using DeviceService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeviceService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Service")]
public class DevicesController : ControllerBase
{
    private readonly IDeviceService _deviceService;

    public DevicesController(IDeviceService deviceService)
    {
        _deviceService = deviceService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllDevices()
    {
        var devices = await _deviceService.GetAllDevicesAsync();
        return Ok(devices);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDevice(int id)
    {
        var device = await _deviceService.GetDeviceByIdAsync(id);
        if (device == null)
            return NotFound();

        return Ok(device);
    }

    [HttpGet("customer/{customerId}")]
    public async Task<IActionResult> GetDevicesByCustomer(int customerId)
    {
        var devices = await _deviceService.GetDevicesByCustomerAsync(customerId);
        return Ok(devices);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDevice([FromBody] CreateDeviceRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var device = await _deviceService.CreateDeviceAsync(
            request.CustomerId,
            request.Brand,
            request.Model,
            request.SerialNumber,
            request.ComplaintDescription);

        return CreatedAtAction(nameof(GetDevice), new { id = device.Id }, device);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDevice(int id, [FromBody] UpdateDeviceRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            await _deviceService.UpdateDeviceAsync(id,
                request.Brand,
                request.Model,
                request.SerialNumber,
                request.ComplaintDescription);

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDevice(int id)
    {
        try
        {
            await _deviceService.DeleteDeviceAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

public class CreateDeviceRequest
{
    public int CustomerId { get; set; }
    public string Brand { get; set; } = null!;
    public string Model { get; set; } = null!;
    public string? SerialNumber { get; set; }
    public string ComplaintDescription { get; set; } = null!;
}

public class UpdateDeviceRequest
{
    public string Brand { get; set; } = null!;
    public string Model { get; set; } = null!;
    public string? SerialNumber { get; set; }
    public string ComplaintDescription { get; set; } = null!;
}
