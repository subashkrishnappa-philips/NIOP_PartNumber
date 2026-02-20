using System.Net.Http.Json;
using System.Text.Json;
using NIOP.Contracts.Shared.Models;

namespace NIOP.Contracts.Shared.Client;

/// <summary>
/// HTTP client wrapper for consuming the NIOP Beat Inventory API.
/// Each consuming system (Salesforce, PCAW, etc.) uses this client
/// to interact with the UpdateDeviceInformation endpoint.
/// 
/// In production, each consumer team would have their own implementation.
/// This shared client is used in contract tests to define the expected interaction.
/// </summary>
public class NiopInventoryApiClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Explicit JSON serializer options to ensure consistent PascalCase property naming
    /// across all .NET versions. This matches the provider's expected contract format.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null, // Preserve PascalCase property names
        PropertyNameCaseInsensitive = true
    };

    public NiopInventoryApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Calls the UpdateDeviceInformation endpoint on the NIOP provider API.
    /// </summary>
    /// <param name="request">The update request.</param>
    /// <returns>The update response.</returns>
    public async Task<HttpResponseMessage> UpdateDeviceInformationAsync(UpdateDeviceInformationRequest request)
    {
        return await _httpClient.PostAsJsonAsync("/api/UpdateDeviceInformation", request, JsonOptions);
    }
}
