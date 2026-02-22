using Microsoft.AspNetCore.Mvc;
using NIOP.Provider.Api.Models;
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
    /// This endpoint is consumed by multiple systems (e.g., PCAW).
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

        try
        {
            var response = await _deviceService.UpdateDeviceInformationAsync(request);

            if (!response.Success)
            {
                // Log the validation failure before returning BadRequest
                _logger.LogWarning("Validation failed for UpdateDeviceInformation: {Message}", response.Message);
                return BadRequest(response);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred in UpdateDeviceInformation for SerialNumber: {SerialNumber}", request.SerialNumber);
            return StatusCode(StatusCodes.Status500InternalServerError, new UpdateDeviceInformationResponse
            {
                Success = false,
                Message = "An internal server error occurred. Please try again later.",
                CorrelationId = Guid.NewGuid().ToString()
            });
        }
    }
}
