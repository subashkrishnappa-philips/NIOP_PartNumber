using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NIOP.Contracts.Shared.Client;
using NIOP.Contracts.Shared.Constants;
using NIOP.Contracts.Shared.Models;
using PactNet;
using Xunit;
using Xunit.Abstractions;

namespace Consumer.ATS.ContractTests;

/// <summary>
/// ATS (Automated Testing System) Consumer Pact Tests.
/// 
/// ATS consumes UpdateDeviceInformation for automated device testing workflows.
/// When devices undergo part number changes during manufacturing or refurbishment,
/// ATS ensures the updated information propagates to testing pipelines.
/// </summary>
public class AtsUpdateDeviceTests
{
    private readonly ITestOutputHelper _output;
    private readonly IPactBuilderV4 _pactBuilder;

    public AtsUpdateDeviceTests(ITestOutputHelper output)
    {
        _output = output;

        var pactDir = PactConstants.PactOutput.GetPactDirectory();

        var pact = Pact.V4(PactConstants.Consumers.ATS, PactConstants.ProviderName, new PactConfig
        {
            PactDir = pactDir,
            LogLevel = PactLogLevel.Information
        });

        _pactBuilder = pact.WithHttpInteractions();
    }

    [Fact(DisplayName = "ATS: Successfully updates device part number for testing pipeline")]
    public async Task UpdateDeviceInformation_ForTestingPipeline_ReturnsSuccess()
    {
        // Arrange
        _pactBuilder
            .UponReceiving("a request from ATS to update device part number for automated testing")
            .Given("a device with serial number SN-ATS-2024-500 is in testing pipeline")
            .WithRequest(HttpMethod.Post, PactConstants.Endpoints.UpdateDeviceInformation)
            .WithJsonBody(new
            {
                SerialNumber = "SN-ATS-2024-500",
                NewPartNumber = "PN-TEST-DEVICE-V6",
                Username = "ats.automation.pipeline"
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
                SerialNumber = "SN-ATS-2024-500",
                NewPartNumber = "PN-TEST-DEVICE-V6",
                Username = "ats.automation.pipeline"
            });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<UpdateDeviceInformationResponse>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
        });
    }

    [Fact(DisplayName = "ATS: Handles bulk device update with single request")]
    public async Task UpdateDeviceInformation_SingleDeviceInBatch_ReturnsSuccess()
    {
        // Arrange
        _pactBuilder
            .UponReceiving("a request from ATS for bulk device update single item")
            .Given("a device with serial number SN-ATS-2024-501 exists in batch")
            .WithRequest(HttpMethod.Post, PactConstants.Endpoints.UpdateDeviceInformation)
            .WithJsonBody(new
            {
                SerialNumber = "SN-ATS-2024-501",
                NewPartNumber = "PN-BATCH-UPDATE-V1",
                Username = "ats.batch.processor"
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
                SerialNumber = "SN-ATS-2024-501",
                NewPartNumber = "PN-BATCH-UPDATE-V1",
                Username = "ats.batch.processor"
            });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        });
    }
}
