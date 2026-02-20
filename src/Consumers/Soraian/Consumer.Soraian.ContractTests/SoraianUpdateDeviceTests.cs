using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NIOP.Contracts.Shared.Client;
using NIOP.Contracts.Shared.Constants;
using NIOP.Contracts.Shared.Models;
using PactNet;
using Xunit;
using Xunit.Abstractions;

namespace Consumer.Soraian.ContractTests;

/// <summary>
/// Soraian Consumer Pact Tests.
/// 
/// Soraian system consumes the UpdateDeviceInformation API to maintain
/// device registry consistency when part numbers are updated in NIOP.
/// Soraian primarily uses this for inventory reconciliation and device tracking.
/// </summary>
public class SoraianUpdateDeviceTests
{
    private readonly ITestOutputHelper _output;
    private readonly IPactBuilderV4 _pactBuilder;

    public SoraianUpdateDeviceTests(ITestOutputHelper output)
    {
        _output = output;

        var pactDir = PactConstants.PactOutput.GetPactDirectory();

        var pact = Pact.V4(PactConstants.Consumers.Soraian, PactConstants.ProviderName, new PactConfig
        {
            PactDir = pactDir,
            LogLevel = PactLogLevel.Information
        });

        _pactBuilder = pact.WithHttpInteractions();
    }

    [Fact(DisplayName = "Soraian: Successfully updates device for inventory reconciliation")]
    public async Task UpdateDeviceInformation_ForInventoryReconciliation_ReturnsSuccess()
    {
        // Arrange
        _pactBuilder
            .UponReceiving("a request from Soraian to update device info for inventory reconciliation")
            .Given("a device with serial number SN-SOR-2024-200 exists")
            .WithRequest(HttpMethod.Post, PactConstants.Endpoints.UpdateDeviceInformation)
            .WithJsonBody(new
            {
                SerialNumber = "SN-SOR-2024-200",
                NewPartNumber = "PN-HOLTER-MONITOR-V2",
                Username = "soraian.sync.service"
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
                SerialNumber = "SN-SOR-2024-200",
                NewPartNumber = "PN-HOLTER-MONITOR-V2",
                Username = "soraian.sync.service"
            });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<UpdateDeviceInformationResponse>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.CorrelationId.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact(DisplayName = "Soraian: Validates response contains correlation ID for tracking")]
    public async Task UpdateDeviceInformation_ReturnsCorrelationId_ForDistributedTracing()
    {
        // Arrange
        _pactBuilder
            .UponReceiving("a valid update request from Soraian expecting correlation ID")
            .Given("a device with serial number SN-SOR-2024-201 exists")
            .WithRequest(HttpMethod.Post, PactConstants.Endpoints.UpdateDeviceInformation)
            .WithJsonBody(new
            {
                SerialNumber = "SN-SOR-2024-201",
                NewPartNumber = "PN-ECG-PATCH-V4",
                Username = "soraian.tracking"
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
                SerialNumber = "SN-SOR-2024-201",
                NewPartNumber = "PN-ECG-PATCH-V4",
                Username = "soraian.tracking"
            });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<UpdateDeviceInformationResponse>();
            result!.CorrelationId.Should().NotBeNullOrEmpty("Soraian requires correlation ID for distributed tracing");
        });
    }
}
