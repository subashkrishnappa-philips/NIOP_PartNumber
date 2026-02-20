using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NIOP.Contracts.Shared.Client;
using NIOP.Contracts.Shared.Constants;
using NIOP.Contracts.Shared.Models;
using PactNet;
using Xunit;
using Xunit.Abstractions;

namespace Consumer.MSA.ContractTests;

/// <summary>
/// MSA (Mobile Services Application) Consumer Pact Tests.
/// 
/// MSA consumes UpdateDeviceInformation to update device records
/// during mobile field operations, including device swap and replacement scenarios.
/// MSA is used by mobile technicians and requires reliable part number updates.
/// </summary>
public class MsaUpdateDeviceTests
{
    private readonly ITestOutputHelper _output;
    private readonly IPactBuilderV4 _pactBuilder;

    public MsaUpdateDeviceTests(ITestOutputHelper output)
    {
        _output = output;

        var pactDir = PactConstants.PactOutput.GetPactDirectory();

        var pact = Pact.V4(PactConstants.Consumers.MSA, PactConstants.ProviderName, new PactConfig
        {
            PactDir = pactDir,
            LogLevel = PactLogLevel.Information
        });

        _pactBuilder = pact.WithHttpInteractions();
    }

    [Fact(DisplayName = "MSA: Successfully updates device during mobile field operation")]
    public async Task UpdateDeviceInformation_DuringFieldOperation_ReturnsSuccess()
    {
        // Arrange
        _pactBuilder
            .UponReceiving("a request from MSA to update device during field swap operation")
            .Given("a device with serial number SN-MSA-2024-300 exists")
            .WithRequest(HttpMethod.Post, PactConstants.Endpoints.UpdateDeviceInformation)
            .WithJsonBody(new
            {
                SerialNumber = "SN-MSA-2024-300",
                NewPartNumber = "PN-MOBILE-SENSOR-V5",
                Username = "msa.field.technician"
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
                SerialNumber = "SN-MSA-2024-300",
                NewPartNumber = "PN-MOBILE-SENSOR-V5",
                Username = "msa.field.technician"
            });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<UpdateDeviceInformationResponse>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
        });
    }

    [Fact(DisplayName = "MSA: Receives error when username is missing")]
    public async Task UpdateDeviceInformation_WithMissingUsername_ReturnsBadRequest()
    {
        // Arrange
        _pactBuilder
            .UponReceiving("a request from MSA with missing username during field update")
            .Given("an update request with empty username")
            .WithRequest(HttpMethod.Post, PactConstants.Endpoints.UpdateDeviceInformation)
            .WithJsonBody(new
            {
                SerialNumber = "SN-MSA-2024-300",
                NewPartNumber = "PN-MOBILE-SENSOR-V5",
                Username = ""
            })
            .WillRespond()
            .WithStatus(HttpStatusCode.BadRequest)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new
            {
                Success = false,
                Message = "Username is required."
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            // Act
            var client = new NiopInventoryApiClient(new HttpClient { BaseAddress = ctx.MockServerUri });
            var response = await client.UpdateDeviceInformationAsync(new UpdateDeviceInformationRequest
            {
                SerialNumber = "SN-MSA-2024-300",
                NewPartNumber = "PN-MOBILE-SENSOR-V5",
                Username = ""
            });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        });
    }
}
