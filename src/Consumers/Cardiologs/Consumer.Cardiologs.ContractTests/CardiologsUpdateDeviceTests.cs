using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NIOP.Contracts.Shared.Client;
using NIOP.Contracts.Shared.Constants;
using NIOP.Contracts.Shared.Models;
using PactNet;
using Xunit;
using Xunit.Abstractions;

namespace Consumer.Cardiologs.ContractTests;

/// <summary>
/// Cardiologs Consumer Pact Tests.
/// 
/// Cardiologs is an AI-powered cardiac diagnostics platform that consumes
/// UpdateDeviceInformation to maintain accurate device-to-patient mappings.
/// When cardiac monitoring devices receive new part numbers, Cardiologs must
/// be notified to update its analytics and reporting pipelines.
/// </summary>
public class CardiologsUpdateDeviceTests
{
    private readonly ITestOutputHelper _output;
    private readonly IPactBuilderV4 _pactBuilder;

    public CardiologsUpdateDeviceTests(ITestOutputHelper output)
    {
        _output = output;

        var pactDir = PactConstants.PactOutput.GetPactDirectory();

        var pact = Pact.V4(PactConstants.Consumers.Cardiologs, PactConstants.ProviderName, new PactConfig
        {
            PactDir = pactDir,
            LogLevel = PactLogLevel.Information
        });

        _pactBuilder = pact.WithHttpInteractions();
    }

    [Fact(DisplayName = "Cardiologs: Successfully updates cardiac device part number")]
    public async Task UpdateDeviceInformation_ForCardiacDevice_ReturnsSuccess()
    {
        // Arrange
        _pactBuilder
            .UponReceiving("a request from Cardiologs to update cardiac monitoring device part number")
            .Given("a cardiac device with serial number SN-CL-2024-600 exists")
            .WithRequest(HttpMethod.Post, PactConstants.Endpoints.UpdateDeviceInformation)
            .WithJsonBody(new
            {
                SerialNumber = "SN-CL-2024-600",
                NewPartNumber = "PN-CARDIO-AI-ECG-V4",
                Username = "cardiologs.analytics.engine"
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
                SerialNumber = "SN-CL-2024-600",
                NewPartNumber = "PN-CARDIO-AI-ECG-V4",
                Username = "cardiologs.analytics.engine"
            });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<UpdateDeviceInformationResponse>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Message.Should().NotBeNullOrEmpty();
        });
    }

    [Fact(DisplayName = "Cardiologs: Validates response message field is present")]
    public async Task UpdateDeviceInformation_ResponseContainsMessage_ForDiagnosticsLogging()
    {
        // Arrange
        _pactBuilder
            .UponReceiving("a valid Cardiologs update verifying message field presence")
            .Given("a cardiac device with serial number SN-CL-2024-601 exists")
            .WithRequest(HttpMethod.Post, PactConstants.Endpoints.UpdateDeviceInformation)
            .WithJsonBody(new
            {
                SerialNumber = "SN-CL-2024-601",
                NewPartNumber = "PN-HOLTER-PATCH-V3",
                Username = "cardiologs.integration"
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
                SerialNumber = "SN-CL-2024-601",
                NewPartNumber = "PN-HOLTER-PATCH-V3",
                Username = "cardiologs.integration"
            });

            // Assert
            var result = await response.Content.ReadFromJsonAsync<UpdateDeviceInformationResponse>();
            result!.Message.Should().NotBeNullOrEmpty("Cardiologs requires message for diagnostics logging");
        });
    }
}
