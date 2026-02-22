namespace NIOP.Provider.Api.Services;

using NIOP.Provider.Api.Models;

/// <summary>
/// Implementation of device service for NIOP inventory operations.
/// In production, this would connect to the actual NIOP database/backend.
/// </summary>
public class DeviceService : IDeviceService
{
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(ILogger<DeviceService> logger)
    {
        _logger = logger;
    }

    public async Task<UpdateDeviceInformationResponse> UpdateDeviceInformationAsync(UpdateDeviceInformationRequest request)
    {
        _logger.LogInformation(
            "Updating device information for SerialNumber: {SerialNumber}, NewPartNumber: {NewPartNumber}, by User: {Username}",
            request.SerialNumber, request.NewPartNumber, request.Username);

        // Validate request
        if (string.IsNullOrWhiteSpace(request.SerialNumber))
        {
            return new UpdateDeviceInformationResponse
            {
                Success = false,
                Message = "Serial number is required.",
                CorrelationId = Guid.NewGuid().ToString()
            };
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return new UpdateDeviceInformationResponse
            {
                Success = false,
                Message = "Username is required.",
                CorrelationId = Guid.NewGuid().ToString()
            };
        }

        if (string.IsNullOrWhiteSpace(request.NewPartNumber))
        {
            return new UpdateDeviceInformationResponse
            {
                Success = false,
                Message = "New part number is required.",
                CorrelationId = Guid.NewGuid().ToString()
            };
        }

        if (string.IsNullOrWhiteSpace(request.Org))
        {
            return new UpdateDeviceInformationResponse
            {
                Success = false,
                Message = "Org is required.",
                CorrelationId = Guid.NewGuid().ToString()
            };
        }

        // Simulate async operation (database call in production)
        await Task.CompletedTask;

        return new UpdateDeviceInformationResponse
        {
            Success = true,
            Message = "Device information updated successfully.",
            CorrelationId = Guid.NewGuid().ToString()
        };
    }
}
