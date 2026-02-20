using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NIOP.Contracts.Shared.Client;
using NIOP.Contracts.Shared.Constants;
using NIOP.Contracts.Shared.Models;
using PactNet;
using PactNet.Matchers;
using Xunit;
using Xunit.Abstractions;

namespace Consumer.PCAW.ContractTests;

/// <summary>
/// PCAW (Patient Care Application Workflow) Consumer Pact Tests.
/// 
/// PCAW uses UpdateDeviceInformation to synchronize part number changes
/// when clinical workflows require device configuration updates.
/// PCAW is a primary consumer that triggers updates during patient care workflows.
/// </summary>
public class PcawUpdateDeviceTests
{
    private readonly ITestOutputHelper _output;
    private readonly IPactBuilderV4 _pactBuilder;

    public PcawUpdateDeviceTests(ITestOutputHelper output)
    {
        _output = output;

        var pactDir = PactConstants.PactOutput.GetPactDirectory();

        var pact = Pact.V4(PactConstants.Consumers.PCAW, PactConstants.ProviderName, new PactConfig
        {
            PactDir = pactDir,
            LogLevel = PactLogLevel.Information
        });

        _pactBuilder = pact.WithHttpInteractions();
    }

    [Fact(DisplayName = "PCAW: Successfully updates device part number during patient workflow")]
    public async Task UpdateDeviceInformation_DuringPatientWorkflow_ReturnsSuccess()
    {
        // Arrange
        _pactBuilder
            .UponReceiving("a request from PCAW to update device part number during patient care workflow")
            .Given("a device with serial number SN-PCAW-2024-100 exists in inventory")
            .WithRequest(HttpMethod.Post, PactConstants.Endpoints.UpdateDeviceInformation)
            .WithJsonBody(new
            {
                SerialNumber = "SN-PCAW-2024-100",
                Username = "pcaw.workflow.engine",
                Org = "philips"
            })
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new
            {
                Success = true,
                Message = "Device information updated successfully.",
                CorrelationId = Match.Type("test-correlation-id-001")
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            // Act
            var client = new NiopInventoryApiClient(new HttpClient { BaseAddress = ctx.MockServerUri });
            var response = await client.UpdateDeviceInformationAsync(new UpdateDeviceInformationRequest
            {
                SerialNumber = "SN-PCAW-2024-100",
                Username = "pcaw.workflow.engine",
                Org = "philips"
            });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<UpdateDeviceInformationResponse>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
        });
    }

    [Fact(DisplayName = "PCAW: Receives error when serial number is empty")]
    public async Task UpdateDeviceInformation_WithEmptySerialNumber_ReturnsBadRequest()
    {
        // Arrange
        _pactBuilder
            .UponReceiving("a request from PCAW with missing serial number")
            .Given("an update request with empty serial number")
            .WithRequest(HttpMethod.Post, PactConstants.Endpoints.UpdateDeviceInformation)
            .WithJsonBody(new
            {
                SerialNumber = "",
                Username = "pcaw.workflow.engine",
                Org = "philips"
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
                Username = "pcaw.workflow.engine",
                Org = "philips"
            });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var result = await response.Content.ReadFromJsonAsync<UpdateDeviceInformationResponse>();
            result.Should().NotBeNull();
            result!.Success.Should().BeFalse();
        });
    }
}
