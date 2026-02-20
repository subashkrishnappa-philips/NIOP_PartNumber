using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NIOP.Contracts.Shared.Client;
using NIOP.Contracts.Shared.Constants;
using NIOP.Contracts.Shared.Models;
using PactNet;
using Xunit;
using Xunit.Abstractions;

namespace Consumer.EMR.ContractTests;

/// <summary>
/// EMR (Electronic Medical Records) Consumer Pact Tests.
/// 
/// EMR system consumes UpdateDeviceInformation to keep patient records
/// synchronized with the correct device part numbers. When devices undergo
/// part number changes, EMR must reflect the updated information in
/// patient-device associations and compliance records.
/// 
/// This is a compliance-critical integration - EMR has the strictest 
/// requirements for response completeness.
/// </summary>
public class EmrUpdateDeviceTests
{
    private readonly ITestOutputHelper _output;
    private readonly IPactBuilderV4 _pactBuilder;

    public EmrUpdateDeviceTests(ITestOutputHelper output)
    {
        _output = output;

        var pactDir = PactConstants.PactOutput.GetPactDirectory();

        var pact = Pact.V4(PactConstants.Consumers.EMR, PactConstants.ProviderName, new PactConfig
        {
            PactDir = pactDir,
            LogLevel = PactLogLevel.Information
        });

        _pactBuilder = pact.WithHttpInteractions();
    }

    [Fact(DisplayName = "EMR: Successfully updates device for patient record synchronization")]
    public async Task UpdateDeviceInformation_ForPatientRecordSync_ReturnsSuccess()
    {
        // Arrange
        _pactBuilder
            .UponReceiving("a request from EMR to update device part number for patient record sync")
            .Given("a device with serial number SN-EMR-2024-700 is linked to patient records")
            .WithRequest(HttpMethod.Post, PactConstants.Endpoints.UpdateDeviceInformation)
            .WithJsonBody(new
            {
                SerialNumber = "SN-EMR-2024-700",
                NewPartNumber = "PN-PATIENT-MONITOR-V8",
                Username = "emr.record.sync"
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
                SerialNumber = "SN-EMR-2024-700",
                NewPartNumber = "PN-PATIENT-MONITOR-V8",
                Username = "emr.record.sync"
            });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<UpdateDeviceInformationResponse>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Message.Should().NotBeNullOrEmpty();
            result.CorrelationId.Should().NotBeNullOrEmpty("EMR requires correlation ID for compliance audit trail");
        });
    }

    [Fact(DisplayName = "EMR: Validates complete response structure for compliance")]
    public async Task UpdateDeviceInformation_ResponseHasAllRequiredFields_ForCompliance()
    {
        // Arrange
        _pactBuilder
            .UponReceiving("a valid EMR update requesting complete response for compliance audit")
            .Given("a device with serial number SN-EMR-2024-701 exists in compliance registry")
            .WithRequest(HttpMethod.Post, PactConstants.Endpoints.UpdateDeviceInformation)
            .WithJsonBody(new
            {
                SerialNumber = "SN-EMR-2024-701",
                NewPartNumber = "PN-COMPLIANCE-DEVICE-V2",
                Username = "emr.compliance.audit"
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
                SerialNumber = "SN-EMR-2024-701",
                NewPartNumber = "PN-COMPLIANCE-DEVICE-V2",
                Username = "emr.compliance.audit"
            });

            // Assert - EMR requires all fields for regulatory compliance
            var result = await response.Content.ReadFromJsonAsync<UpdateDeviceInformationResponse>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Message.Should().NotBeNullOrEmpty("Message is required for EMR compliance records");
            result.CorrelationId.Should().NotBeNullOrEmpty("CorrelationId is required for EMR audit trail");
        });
    }

    [Fact(DisplayName = "EMR: Receives structured error for missing serial number")]
    public async Task UpdateDeviceInformation_WithMissingSerial_ReturnsBadRequestWithMessage()
    {
        // Arrange
        _pactBuilder
            .UponReceiving("a request from EMR with missing serial number expecting structured error")
            .Given("an update request with empty serial number for EMR")
            .WithRequest(HttpMethod.Post, PactConstants.Endpoints.UpdateDeviceInformation)
            .WithJsonBody(new
            {
                SerialNumber = "",
                NewPartNumber = "PN-PATIENT-MONITOR-V8",
                Username = "emr.record.sync"
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
                NewPartNumber = "PN-PATIENT-MONITOR-V8",
                Username = "emr.record.sync"
            });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var result = await response.Content.ReadFromJsonAsync<UpdateDeviceInformationResponse>();
            result.Should().NotBeNull();
            result!.Success.Should().BeFalse();
            result.Message.Should().NotBeNullOrEmpty("EMR requires structured error messages");
        });
    }
}
