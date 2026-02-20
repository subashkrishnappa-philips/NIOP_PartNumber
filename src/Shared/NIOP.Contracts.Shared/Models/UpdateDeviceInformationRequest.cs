namespace NIOP.Contracts.Shared.Models;

/// <summary>
/// Request model for UpdateDeviceInformation API endpoint.
/// Used by consuming systems (Salesforce, PCAW, Soraian, MSA, INR, ATS, Cardiologs, EMR)
/// to update device information with new part numbers in the NIOP inventory system.
/// </summary>
public class UpdateDeviceInformationRequest
{
    /// <summary>
    /// The serial number of the device to update.
    /// Must be a valid, existing serial number in the NIOP inventory.
    /// </summary>
    /// <example>SN-2024-001234</example>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>
    /// The new part number to assign to the device.
    /// This is the field impacted by NIOP part number changes.
    /// </summary>
    /// <example>PN-BEAT-5678-REV2</example>
    public string NewPartNumber { get; set; } = string.Empty;

    /// <summary>
    /// The username of the person/system performing the update.
    /// Used for audit trail and change tracking.
    /// </summary>
    /// <example>system.salesforce</example>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// The organization associated with the device update.
    /// Required field introduced for organizational tracking.
    /// </summary>
    /// <example>philips</example>
    public string Org { get; set; } = string.Empty;
}
