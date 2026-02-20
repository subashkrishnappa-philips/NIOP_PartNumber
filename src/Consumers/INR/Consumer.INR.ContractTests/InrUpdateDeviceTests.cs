using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NIOP.Contracts.Shared.Client;
using NIOP.Contracts.Shared.Constants;
using NIOP.Contracts.Shared.Models;
using PactNet;
using Xunit;
using Xunit.Abstractions;

namespace Consumer.INR.ContractTests;

/// <summary>
/// INR (International Normalized Ratio System) Consumer Pact Tests.
/// 
/// INR system consumes UpdateDeviceInformation for managing INR monitoring
/// device part numbers. Critical for anticoagulation therapy device tracking.
/// INR updates are typically triggered during device calibration workflows.
/// </summary>
public class InrUpdateDeviceTests
{
    private readonly ITestOutputHelper _output;
    private readonly IPactBuilderV4 _pactBuilder;

    public InrUpdateDeviceTests(ITestOutputHelper output)
    {
        _output = output;

        var pactDir = PactConstants.PactOutput.GetPactDirectory();

        var pact = Pact.V4(PactConstants.Consumers.INR, PactConstants.ProviderName, new PactConfig
        {
            PactDir = pactDir,
            LogLevel = PactLogLevel.Information
        });

        _pactBuilder = pact.WithHttpInteractions();
    }

    [Fact(DisplayName = "INR: Successfully updates INR monitoring device")]
    public async Task UpdateDeviceInformation_ForInrDevice_ReturnsSuccess()
    {
        // Arrange
        _pactBuilder
            .UponReceiving("a request from INR system to update INR monitoring device part number")
            .Given("an INR device with serial number SN-INR-2024-400 exists")
            .WithRequest(HttpMethod.Post, PactConstants.Endpoints.UpdateDeviceInformation)
            .WithJsonBody(new
            {
                SerialNumber = "SN-INR-2024-400",
                NewPartNumber = "PN-INR-MONITOR-V3",
                Username = "inr.calibration.service"
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
                SerialNumber = "SN-INR-2024-400",
                NewPartNumber = "PN-INR-MONITOR-V3",
                Username = "inr.calibration.service"
            });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<UpdateDeviceInformationResponse>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
        });
    }

    [Fact(DisplayName = "INR: Verifies Success field is boolean in response")]
    public async Task UpdateDeviceInformation_ResponseSuccessField_IsBoolean()
    {
        // Arrange
        _pactBuilder
            .UponReceiving("a valid INR update request verifying boolean Success field")
            .Given("an INR device with serial number SN-INR-2024-401 exists")
            .WithRequest(HttpMethod.Post, PactConstants.Endpoints.UpdateDeviceInformation)
            .WithJsonBody(new
            {
                SerialNumber = "SN-INR-2024-401",
                NewPartNumber = "PN-INR-COAG-V2",
                Username = "inr.system"
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
                SerialNumber = "SN-INR-2024-401",
                NewPartNumber = "PN-INR-COAG-V2",
                Username = "inr.system"
            });

            // Assert
            var result = await response.Content.ReadFromJsonAsync<UpdateDeviceInformationResponse>();
            result!.Success.Should().BeTrue("INR system requires a clear boolean success indicator");
        });
    }
}
