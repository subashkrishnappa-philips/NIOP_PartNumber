using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using NIOP.Contracts.Shared.Models;
using NIOP.Provider.Api.Services;

namespace NIOP.Provider.ContractTests.Fixtures;

/// <summary>
/// Custom WebApplicationFactory for Provider contract tests.
/// Sets up the provider API with mocked dependencies for Pact verification.
/// 
/// PactNet verifier requires a real HTTP endpoint (not an in-memory TestServer),
/// so we configure Kestrel to listen on a random available port.
/// </summary>
public class ProviderWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly int _port;
    private IHost? _kestrelHost;

    /// <summary>
    /// The URI where the provider is listening on a real network port.
    /// </summary>
    public Uri ServerUri => new($"http://localhost:{_port}");

    public Mock<IDeviceService> MockDeviceService { get; } = new();

    public ProviderWebApplicationFactory()
    {
        // Find a free port
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        _port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
    }

    /// <summary>
    /// Starts the real Kestrel host. Must be called before running PactVerifier.
    /// </summary>
    public void EnsureStarted()
    {
        if (_kestrelHost != null) return;

        // Setup default mock behavior for successful update
        MockDeviceService
            .Setup(s => s.UpdateDeviceInformationAsync(It.IsAny<UpdateDeviceInformationRequest>()))
            .ReturnsAsync(new UpdateDeviceInformationResponse
            {
                Success = true,
                Message = "Device information updated successfully.",
                CorrelationId = "test-correlation-id-001"
            });

        // Setup mock for invalid serial number (empty)
        MockDeviceService
            .Setup(s => s.UpdateDeviceInformationAsync(
                It.Is<UpdateDeviceInformationRequest>(r => string.IsNullOrWhiteSpace(r.SerialNumber))))
            .ReturnsAsync(new UpdateDeviceInformationResponse
            {
                Success = false,
                Message = "Serial number is required.",
                CorrelationId = "test-correlation-id-error-001"
            });

        // Setup mock for empty part number
        MockDeviceService
            .Setup(s => s.UpdateDeviceInformationAsync(
                It.Is<UpdateDeviceInformationRequest>(r => string.IsNullOrWhiteSpace(r.NewPartNumber))))
            .ReturnsAsync(new UpdateDeviceInformationResponse
            {
                Success = false,
                Message = "New part number is required.",
                CorrelationId = "test-correlation-id-error-002"
            });

        // Setup mock for empty username
        MockDeviceService
            .Setup(s => s.UpdateDeviceInformationAsync(
                It.Is<UpdateDeviceInformationRequest>(r => string.IsNullOrWhiteSpace(r.Username))))
            .ReturnsAsync(new UpdateDeviceInformationResponse
            {
                Success = false,
                Message = "Username is required.",
                CorrelationId = "test-correlation-id-error-003"
            });

        // Setup mock for empty org
        MockDeviceService
            .Setup(s => s.UpdateDeviceInformationAsync(
                It.Is<UpdateDeviceInformationRequest>(r => string.IsNullOrWhiteSpace(r.Org))))
            .ReturnsAsync(new UpdateDeviceInformationResponse
            {
                Success = false,
                Message = "Org is required.",
                CorrelationId = "test-correlation-id-error-004"
            });

        _kestrelHost = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls($"http://localhost:{_port}");
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers()
                        .AddJsonOptions(options =>
                        {
                            options.JsonSerializerOptions.PropertyNamingPolicy = null;
                            options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                        })
                        .AddApplicationPart(typeof(NIOP.Provider.Api.Controllers.DeviceController).Assembly);

                    services.AddScoped<IDeviceService>(_ => MockDeviceService.Object);
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            })
            .Build();

        _kestrelHost.Start();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _kestrelHost?.StopAsync().GetAwaiter().GetResult();
            (_kestrelHost as IDisposable)?.Dispose();
        }
        base.Dispose(disposing);
    }
}
