using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NIOP.Provider.Api.Services;
using NIOP.Provider.ContractTests.Constants;
using NIOP.Provider.ContractTests.Validation;
using Xunit;
using Xunit.Abstractions;

namespace NIOP.Provider.ContractTests;

/// <summary>
/// Cross-layer validation: ensures every Pact consumer contract is compatible
/// with the provider's published OpenAPI (Swagger) specification.
///
/// Why this matters:
///   Pact tests verify request/response interactions in isolation.
///   This validator additionally checks that the interaction schemas match what
///   the Swagger spec documents — catching drift like renamed fields, changed
///   types, or undocumented status codes before they reach production.
///
/// Implemented entirely in C# using Microsoft.OpenApi.Readers — no Node.js,
/// no npm, no npx required.
///
/// Typical failure scenarios caught:
///   - Pact body uses a property name not present in the Swagger schema
///   - Swagger marks a field Required but the Pact omits it
///   - HTTP status code in Pact not declared in the Swagger responses
///   - Property type mismatch (e.g. Pact sends a number where spec says string)
/// </summary>
public class SwaggerMockValidatorTests
{
    private readonly ITestOutputHelper _output;

    public SwaggerMockValidatorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Fetches swagger.json from an in-process test server, finds all local Pact
    /// files, then validates each one entirely in C# using
    /// <see cref="PactSwaggerValidator"/>.
    /// </summary>
    [Fact(DisplayName = "Pact contracts are compatible with the OpenAPI/Swagger specification")]
    public async Task PactContracts_AreCompatibleWithSwaggerSpec()
    {
        // ── Step 1: Fetch swagger.json from the in-process provider ──────────────
        _output.WriteLine("Starting in-process provider test server to fetch swagger.json...");
        var swaggerJson = await FetchSwaggerJsonAsync();
        _output.WriteLine("swagger.json fetched successfully.");

        // ── Step 2: Locate Pact files produced by consumer tests ─────────────────
        var pactDir = PactConstants.PactOutput.GetPactDirectory();

        if (!Directory.Exists(pactDir))
            Assert.Fail(
                $"Pact directory not found at '{pactDir}'. " +
                "Run the consumer contract tests first: " +
                "cd NIOP_PARTNUMBERENDPOINTS_CONSUMER && dotnet test");

        var pactFiles = Directory.GetFiles(pactDir, "*.json", SearchOption.TopDirectoryOnly);

        Assert.True(pactFiles.Length > 0,
            $"No pact files found in '{pactDir}'. " +
            "Run the consumer contract tests to generate them first.");

        _output.WriteLine($"\nFound {pactFiles.Length} pact file(s):");
        foreach (var f in pactFiles)
            _output.WriteLine($"  {Path.GetFileName(f)}");

        // ── Step 3: Validate each Pact file against the Swagger spec (pure C#) ───
        var validator   = new PactSwaggerValidator();
        var allFailures = new List<string>();

        foreach (var pactFile in pactFiles)
        {
            _output.WriteLine($"\n── Validating: {Path.GetFileName(pactFile)} ──");
            var pactJson = await File.ReadAllTextAsync(pactFile);

            var results = validator.Validate(swaggerJson, pactJson, Path.GetFileName(pactFile));

            foreach (var result in results)
            {
                if (result.IsValid)
                {
                    _output.WriteLine($"  [PASS] {result.InteractionDescription}");
                }
                else
                {
                    _output.WriteLine($"  [FAIL] {result.InteractionDescription}");
                    foreach (var error in result.Errors)
                        _output.WriteLine($"         • {error}");

                    allFailures.Add(
                        $"Pact: {result.PactFile}\n" +
                        $"Interaction: {result.InteractionDescription}\n" +
                        string.Join("\n", result.Errors.Select(e => $"  • {e}")));
                }
            }
        }

        // ── Step 4: Assert all interactions passed ────────────────────────────────
        Assert.True(allFailures.Count == 0,
            $"swagger-mock-validator (C#) found {allFailures.Count} incompatible " +
            $"Pact interaction(s):\n\n" +
            string.Join("\n\n", allFailures));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spins up an in-process WebApplicationFactory in Development mode,
    /// which activates the <c>app.UseSwagger()</c> middleware defined in Program.cs,
    /// then downloads <c>/swagger/v1/swagger.json</c>.
    /// </summary>
    private async Task<string> FetchSwaggerJsonAsync()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // Development mode activates app.UseSwagger() / app.UseSwaggerUI() in Program.cs
                builder.UseEnvironment("Development");

                builder.ConfigureServices(services =>
                {
                    // Replace the real DeviceService with a no-op mock — no real
                    // infrastructure is needed to generate the Swagger spec.
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IDeviceService));
                    if (descriptor != null)
                        services.Remove(descriptor);

                    var mockService = new Mock<IDeviceService>();
                    services.AddScoped<IDeviceService>(_ => mockService.Object);
                });
            });

        using var client = factory.CreateClient();
        return await client.GetStringAsync("/swagger/v1/swagger.json");
    }
}
