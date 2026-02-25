using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NIOP.Provider.Api.Services;

namespace NIOP.Provider.ContractTests.Fixtures;

/// <summary>
/// Custom WebApplicationFactory for Provider contract tests.
/// Sets up the provider API with real services for Pact verification.
///
/// PactNet verifier requires a real HTTP endpoint (not an in-memory TestServer),
/// so we configure Kestrel to listen on a random available port.
///
/// The real DeviceService is used intentionally — it has no external dependencies
/// (pure in-memory validation, no database), so it is safe to use in tests and
/// avoids brittle Moq predicate-matching that can fail when JSON deserialization
/// produces empty strings instead of null on some runtimes.
/// </summary>
public class ProviderWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly int _port;
    private IHost? _kestrelHost;

    /// <summary>
    /// The URI where the provider is listening on a real network port.
    /// </summary>
    public Uri ServerUri => new($"http://localhost:{_port}");

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

                    // Suppress ASP.NET Core's automatic 400 ValidationProblemDetails response
                    // so requests with empty/missing required fields reach DeviceService,
                    // which returns the { Success, Message, CorrelationId } format Pact consumers expect.
                    services.Configure<ApiBehaviorOptions>(options =>
                    {
                        options.SuppressModelStateInvalidFilter = true;
                    });

                    // Use the real DeviceService — it is pure in-memory validation with no
                    // external dependencies, so it is safe and correct in contract tests.
                    services.AddScoped<IDeviceService, DeviceService>();
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
