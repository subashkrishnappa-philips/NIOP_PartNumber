using NIOP.Provider.ContractTests.Constants;
using PactNet;
using PactNet.Verifier;
using Xunit;
using Xunit.Abstractions;

namespace NIOP.Provider.ContractTests;

/// <summary>
/// Provider-side Pact verification tests.
/// 
/// These tests verify that the NIOP Beat Inventory API (Provider) 
/// satisfies ALL consumer contracts (pacts) published to the Pact Broker.
/// 
/// This is executed as part of the provider's CI/CD pipeline.
/// When any consumer publishes a new pact, these tests will detect breaking changes.
/// 
/// Consuming systems verified:
/// - PCAW
/// </summary>
public class ProviderContractTests : IClassFixture<Fixtures.ProviderWebApplicationFactory>
{
    private readonly Fixtures.ProviderWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public ProviderContractTests(Fixtures.ProviderWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    /// <summary>
    /// Verifies all consumer pacts from the Pact Broker against the running provider.
    /// This single test covers ALL consumers - each pact is verified independently.
    /// </summary>
    [Fact(DisplayName = "Provider verifies all consumer pacts from Pact Broker")]
    public void EnsureProviderHonoursAllConsumerPacts()
    {
        // Arrange - Start the real Kestrel-hosted provider
        _factory.EnsureStarted();
        var providerUri = _factory.ServerUri;

        var brokerUrl = Environment.GetEnvironmentVariable(PactConstants.Broker.UrlEnvironmentVariable)
                        ?? PactConstants.Broker.DefaultUrl;

        var brokerToken = Environment.GetEnvironmentVariable(PactConstants.Broker.TokenEnvironmentVariable);
        var brokerUsername = Environment.GetEnvironmentVariable(PactConstants.Broker.UsernameEnvironmentVariable);
        var brokerPassword = Environment.GetEnvironmentVariable(PactConstants.Broker.PasswordEnvironmentVariable);

        // Act & Assert - Verify pacts from broker
        using var verifier = new PactVerifier(PactConstants.ProviderName);

        if (!string.IsNullOrEmpty(brokerToken))
        {
            // Token-based authentication (e.g. PactFlow SaaS)
            verifier
                .WithHttpEndpoint(providerUri)
                .WithPactBrokerSource(new Uri(brokerUrl), options =>
                {
                    options.TokenAuthentication(brokerToken);
                    options.PublishResults(
                        Environment.GetEnvironmentVariable("PROVIDER_VERSION") ?? "1.0.0",
                        results =>
                        {
                            var branch = Environment.GetEnvironmentVariable("GIT_BRANCH") ?? "main";
                            results.ProviderBranch(branch);
                        });
                    options.ConsumerVersionSelectors(
                        new ConsumerVersionSelector { MainBranch = true },
                        new ConsumerVersionSelector { DeployedOrReleased = true },
                        new ConsumerVersionSelector { Latest = true }
                    );
                    options.EnablePending();
                })
                .Verify();
        }
        else if (!string.IsNullOrEmpty(brokerUsername) && !string.IsNullOrEmpty(brokerPassword))
        {
            // Basic auth (e.g. self-hosted Pact Broker via Docker)
            _output.WriteLine($"Verifying pacts from Pact Broker at {brokerUrl} (basic auth)");
            verifier
                .WithHttpEndpoint(providerUri)
                .WithPactBrokerSource(new Uri(brokerUrl), options =>
                {
                    options.BasicAuthentication(brokerUsername, brokerPassword);
                    options.PublishResults(
                        Environment.GetEnvironmentVariable("PROVIDER_VERSION") ?? "1.0.0",
                        results =>
                        {
                            var branch = Environment.GetEnvironmentVariable("GIT_BRANCH") ?? "main";
                            results.ProviderBranch(branch);
                        });
                    options.ConsumerVersionSelectors(
                        new ConsumerVersionSelector { MainBranch = true },
                        new ConsumerVersionSelector { DeployedOrReleased = true },
                        new ConsumerVersionSelector { Latest = true }
                    );
                    options.EnablePending();
                })
                .Verify();
        }
        else
        {
            // In CI, pacts MUST come from the broker (consumer-generated).
            // Local pact files are only used for local development convenience.
            var isCI = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"));
            if (isCI)
            {
                Assert.Fail(
                    "Pact Broker credentials are required in CI. " +
                    "Consumer-generated pact files must be fetched from the Pact Broker. " +
                    "Set PACT_BROKER_BASE_URL and PACT_BROKER_TOKEN (or USERNAME/PASSWORD) environment variables.");
            }

            // Local development fallback: verify from local pact files
            _output.WriteLine("WARNING: No Pact Broker configured. Using local pact files for development only.");
            _output.WriteLine("In CI/CD, pacts should always be fetched from the Pact Broker (consumer-generated).");

            var pactDir = PactConstants.PactOutput.GetPactDirectory();
            var pactFiles = Directory.Exists(pactDir)
                ? Directory.GetFiles(pactDir, "*.json")
                : Array.Empty<string>();

            Assert.True(pactFiles.Length > 0,
                $"No pact files found at '{pactDir}' and no broker configured. " +
                "Run consumer pact tests first to generate pact files, or set PACT_BROKER_* environment variables.");

            _output.WriteLine($"Verifying {pactFiles.Length} local pact file(s) from {pactDir}");
            verifier
                .WithHttpEndpoint(providerUri)
                .WithDirectorySource(new DirectoryInfo(pactDir))
                .Verify();
        }
    }

    /// <summary>
    /// Verifies pacts for a specific consumer (useful for targeted testing).
    /// </summary>
    [Theory(DisplayName = "Provider verifies individual consumer pacts from local files")]
    [InlineData(PactConstants.Consumers.PCAW)]
    public void EnsureProviderHonoursSpecificConsumerPact(string consumerName)
    {
        // Arrange - Start the real Kestrel-hosted provider
        _factory.EnsureStarted();
        var providerUri = _factory.ServerUri;

        var pactDir = PactConstants.PactOutput.GetPactDirectory();
        var pactFile = Path.Combine(pactDir, $"{consumerName}-{PactConstants.ProviderName}.json");

        Assert.True(File.Exists(pactFile),
            $"Pact file not found for consumer '{consumerName}': {pactFile}. " +
            "Run the consumer pact tests first to generate pact files.");

        // Act & Assert
        using var verifier = new PactVerifier(PactConstants.ProviderName);
        verifier
            .WithHttpEndpoint(providerUri)
            .WithFileSource(new FileInfo(pactFile))
            .Verify();
    }
}
