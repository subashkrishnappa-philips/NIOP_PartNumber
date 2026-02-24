using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NIOP.Provider.Api.Models;

/// <summary>
/// Request model for UpdateDeviceInformation API endpoint.
/// Used by consuming systems to update device information
/// with new part numbers in the NIOP inventory system.
/// </summary>
public class UpdateDeviceInformationRequest
{
    /// <summary>
    /// The serial number of the device to update.
    /// Must be a valid, existing serial number in the NIOP inventory.
    /// </summary>
    [Required]
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>
    /// The new part number to assign to the device.
    /// This is the field impacted by NIOP part number changes.
    /// Required by the provider for device updates.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NewPartNumber { get; set; }

    /// <summary>
    /// The username of the person/system performing the update.
    /// Used for audit trail and change tracking.
    /// </summary>
    [Required]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// The organization associated with the device update.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Org { get; set; }
}
