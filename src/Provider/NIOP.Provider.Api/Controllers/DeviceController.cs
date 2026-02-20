using Microsoft.AspNetCore.Mvc;
using NIOP.Contracts.Shared.Models;
using NIOP.Provider.Api.Services;

namespace NIOP.Provider.Api.Controllers;

/// <summary>
/// Controller for device inventory management operations.
/// Part of the Beat.Inventory.Client.Api service.
/// </summary>
[ApiController]
[Route("api")]
public class DeviceController : ControllerBase
{
    private readonly IDeviceService _deviceService;
    private readonly ILogger<DeviceController> _logger;

    public DeviceController(IDeviceService deviceService, ILogger<DeviceController> logger)
    {
        _deviceService = deviceService;
        _logger = logger;
    }

    /// <summary>
    /// Updates device information with a new part number.
    /// This endpoint is consumed by: Salesforce, PCAW, Soraian, MSA, INR, ATS, Cardiologs, EMR.
    /// It is directly impacted by NIOP part number changes.
    /// </summary>
    /// <param name="request">The update device information request.</param>
    /// <returns>A response indicating success or failure.</returns>
    /// <response code="200">Device information updated successfully.</response>
    /// <response code="400">Invalid request - missing or invalid fields.</response>
    /// <response code="500">Internal server error during update.</response>
    [HttpPost("UpdateDeviceInformation")]
    [ProducesResponseType(typeof(UpdateDeviceInformationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UpdateDeviceInformationResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateDeviceInformation([FromBody] UpdateDeviceInformationRequest request)
    {
        _logger.LogInformation("Received UpdateDeviceInformation request for SerialNumber: {SerialNumber}", request.SerialNumber);

        var response = await _deviceService.UpdateDeviceInformationAsync(request);

        if (!response.Success)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }
}
