namespace NIOP.Provider.ContractTests.Constants;

/// <summary>
/// Constants for Provider Pact contract verification.
/// </summary>
public static class PactConstants
{
    /// <summary>
    /// Provider name used in all Pact contracts.
    /// This must match exactly between consumer tests and provider verification.
    /// </summary>
    public const string ProviderName = "NIOP-Beat-Inventory-Api";

    /// <summary>
    /// Consumer names for each consuming system.
    /// </summary>
    public static class Consumers
    {
        public const string PCAW = "PCAW-Consumer";
    }

    /// <summary>
    /// Pact Broker configuration defaults.
    /// Override via environment variables in CI/CD.
    /// </summary>
    public static class Broker
    {
        public const string DefaultUrl = "http://localhost:9292";
        public const string UrlEnvironmentVariable = "PACT_BROKER_BASE_URL";
        public const string TokenEnvironmentVariable = "PACT_BROKER_TOKEN";
        public const string UsernameEnvironmentVariable = "PACT_BROKER_USERNAME";
        public const string PasswordEnvironmentVariable = "PACT_BROKER_PASSWORD";
    }

    /// <summary>
    /// Pact file output configuration.
    /// </summary>
    public static class PactOutput
    {
        public const string DefaultPactDir = "pacts";

        /// <summary>
        /// Resolves the pact output directory by finding the solution root.
        /// Traverses up from the current assembly's location until it finds
        /// the NIOP.Provider.sln file, then returns the pacts subdirectory.
        /// </summary>
        public static string GetPactDirectory()
        {
            var dir = Path.GetDirectoryName(typeof(PactConstants).Assembly.Location);
            while (dir != null && !File.Exists(Path.Combine(dir, "NIOP.Provider.sln")))
            {
                dir = Path.GetDirectoryName(dir);
            }

            if (dir == null)
                throw new DirectoryNotFoundException(
                    "Could not find solution root directory. Ensure NIOP.Provider.sln exists in a parent directory.");

            var pactDir = Path.Combine(dir, DefaultPactDir);
            Directory.CreateDirectory(pactDir);
            return pactDir;
        }
    }
}
