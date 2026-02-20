using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NIOP.Contracts.Shared.Client;
using NIOP.Contracts.Shared.Constants;
using NIOP.Contracts.Shared.Models;
using PactNet;
using Xunit;
using Xunit.Abstractions;

namespace Consumer.Salesforce.ContractTests;

/// <summary>
/// Salesforce Consumer Pact Tests for the UpdateDeviceInformation API.
/// 
/// Salesforce integrates with NIOP to update device part numbers when
/// field service engineers perform device replacements or upgrades.
/// 
/// These tests define what Salesforce expects from the NIOP API
/// and generate a Pact contract file that the provider must satisfy.
/// </summary>
public class SalesforceUpdateDeviceTests
{
    private readonly ITestOutputHelper _output;
    private readonly IPactBuilderV4 _pactBuilder;

    public SalesforceUpdateDeviceTests(ITestOutputHelper output)
    {
        _output = output;

        var pactDir = PactConstants.PactOutput.GetPactDirectory();

        var pact = Pact.V4(PactConstants.Consumers.Salesforce, PactConstants.ProviderName, new PactConfig
        {
            PactDir = pactDir,
            LogLevel = PactLogLevel.Information
        });

        _pactBuilder = pact.WithHttpInteractions();
    }

    [Fact(DisplayName = "Salesforce: Successfully updates device with new part number")]
    public async Task UpdateDeviceInformation_WithValidRequest_ReturnsSuccess()
    {
        // Arrange
        _pactBuilder
            .UponReceiving("a request from Salesforce to update device information with a valid part number")
            .Given("a device with serial number SN-SF-2024-001 exists")
            .WithRequest(HttpMethod.Post, PactConstants.Endpoints.UpdateDeviceInformation)
            .WithJsonBody(new
            {
                SerialNumber = "SN-SF-2024-001",
                NewPartNumber = "PN-BEAT-5678-REV2",
                Username = "salesforce.integration"
            })
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new
            {
                Success = true,
                Message = "Device information updated successfully.",
                CorrelationId = "test-correlation-id-001"
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            // Act
            var client = new NiopInventoryApiClient(new HttpClient { BaseAddress = ctx.MockServerUri });
            var response = await client.UpdateDeviceInformationAsync(new UpdateDeviceInformationRequest
            {
                SerialNumber = "SN-SF-2024-001",
                NewPartNumber = "PN-BEAT-5678-REV2",
                Username = "salesforce.integration"
            });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<UpdateDeviceInformationResponse>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Message.Should().Be("Device information updated successfully.");
        });
    }

    [Fact(DisplayName = "Salesforce: Receives error when serial number is missing")]
    public async Task UpdateDeviceInformation_WithMissingSerialNumber_ReturnsBadRequest()
    {
        // Arrange
        _pactBuilder
            .UponReceiving("a request from Salesforce to update device with missing serial number")
            .Given("an update request with empty serial number")
            .WithRequest(HttpMethod.Post, PactConstants.Endpoints.UpdateDeviceInformation)
            .WithJsonBody(new
            {
                SerialNumber = "",
                NewPartNumber = "PN-BEAT-5678-REV2",
                Username = "salesforce.integration"
            })
            .WillRespond()
            .WithStatus(HttpStatusCode.BadRequest)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new
            {
                Success = false,
                Message = "Serial number is required."
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            // Act
            var client = new NiopInventoryApiClient(new HttpClient { BaseAddress = ctx.MockServerUri });
            var response = await client.UpdateDeviceInformationAsync(new UpdateDeviceInformationRequest
            {
                SerialNumber = "",
                NewPartNumber = "PN-BEAT-5678-REV2",
                Username = "salesforce.integration"
            });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var result = await response.Content.ReadFromJsonAsync<UpdateDeviceInformationResponse>();
            result.Should().NotBeNull();
            result!.Success.Should().BeFalse();
        });
    }
}
