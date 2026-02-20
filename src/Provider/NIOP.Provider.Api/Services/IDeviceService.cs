namespace NIOP.Provider.Api.Services;

using NIOP.Contracts.Shared.Models;

/// <summary>
/// Interface for device-related operations in the NIOP inventory system.
/// </summary>
public interface IDeviceService
{
    /// <summary>
    /// Updates device information including part number changes.
    /// This is the core operation impacted by NIOP part number updates.
    /// </summary>
    /// <param name="request">The update request containing serial number, new part number, and username.</param>
    /// <returns>A response indicating the success or failure of the update.</returns>
    Task<UpdateDeviceInformationResponse> UpdateDeviceInformationAsync(UpdateDeviceInformationRequest request);
}
