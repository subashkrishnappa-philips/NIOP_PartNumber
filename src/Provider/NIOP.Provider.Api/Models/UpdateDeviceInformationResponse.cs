namespace NIOP.Provider.Api.Models;

/// <summary>
/// Response model for UpdateDeviceInformation API endpoint.
/// Returns the result of the device information update operation.
/// </summary>
public class UpdateDeviceInformationResponse
{
    /// <summary>
    /// Indicates whether the device information update was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Optional message providing additional context about the operation result.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Correlation ID for tracking the request across distributed systems.
    /// </summary>
    public string? CorrelationId { get; set; }
}
