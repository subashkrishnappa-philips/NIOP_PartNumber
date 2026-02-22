# Provider Team Guide (NIOP)

> How to verify consumer contracts and safely evolve the Beat Inventory API

---

## Who Is This For?

This guide is for the **NIOP team** who owns and maintains the `Beat.Inventory.Client.Api`, specifically the `POST /api/UpdateDeviceInformation` endpoint.

---

## Your Responsibilities

As the API provider, you must:

1. **Run provider verification** on every code change
2. **Check `can-i-deploy`** before deploying to any environment
3. **Communicate breaking changes** to consumer teams
4. **Set up provider states** for consumer test preconditions
5. **Monitor the Pact Broker** for new consumer contracts

---

## How Provider Verification Works

```
┌──────────────────────────┐
│ Provider Verification     │
│                          │
│ 1. Start your real API   │
│ 2. Fetch pacts from      │
│    Pact Broker           │
│ 3. For each consumer:    │
│    a. Replay request     │
│    b. Compare response   │
│ 4. Report results to     │
│    Pact Broker           │
└──────────────────────────┘
```

### Running Verification Locally

```bash
# Against local pact files (in pacts/ directory)
dotnet test src/Provider/NIOP.Provider.ContractTests/NIOP.Provider.ContractTests.csproj --configuration Release

# Against Pact Broker
$env:PACT_BROKER_BASE_URL = "http://localhost:9292"
$env:PACT_BROKER_USERNAME = "pact_user"
$env:PACT_BROKER_PASSWORD = "pact_password"
$env:PROVIDER_VERSION = "local-dev"
dotnet test src/Provider/NIOP.Provider.ContractTests/NIOP.Provider.ContractTests.csproj --configuration Release

# Only broker-based tests (same as CI pipeline)
dotnet test src/Provider/NIOP.Provider.ContractTests/NIOP.Provider.ContractTests.csproj --filter "DisplayName~Pact Broker"
```

---

## Key Source Files

| File | Purpose |
|------|---------|
| `ProviderContractTests.cs` | Pact verification test methods (broker + local file) |
| `ProviderWebApplicationFactory.cs` | Real Kestrel host with mocked `IDeviceService` |
| `PactConstants.cs` | Provider/consumer names, broker config, pact directory resolution |
| `DeviceController.cs` | The API endpoint under contract |
| `DeviceService.cs` | Business logic with validation rules |
| `UpdateDeviceInformationRequest.cs` | Request model (SerialNumber, NewPartNumber, Username, Org) |
| `UpdateDeviceInformationResponse.cs` | Response model (Success, Message, CorrelationId) |

---

## Managing Provider States

### What Are Provider States?

When a consumer pact says:
```
Given "a device with serial number SN-PCAW-2024-100 exists in inventory"
```

Your provider mock must ensure that state is handled before running the interaction.

### Current Implementation

In `ProviderWebApplicationFactory.cs`, the mock `IDeviceService` is configured with ordered `Setup` calls:

```csharp
// Success: all required fields present (SerialNumber, NewPartNumber, Username, Org)
MockDeviceService.Setup(s => s.UpdateDeviceInformationAsync(
    It.Is<UpdateDeviceInformationRequest>(req =>
        !string.IsNullOrWhiteSpace(req.SerialNumber) &&
        !string.IsNullOrWhiteSpace(req.NewPartNumber) &&
        !string.IsNullOrWhiteSpace(req.Username) &&
        !string.IsNullOrWhiteSpace(req.Org))))
    .ReturnsAsync(new UpdateDeviceInformationResponse { Success = true, ... });

// Error: missing SerialNumber
MockDeviceService.Setup(s => s.UpdateDeviceInformationAsync(
    It.Is<UpdateDeviceInformationRequest>(req =>
        string.IsNullOrWhiteSpace(req.SerialNumber))))
    .ReturnsAsync(new UpdateDeviceInformationResponse { Success = false, ... });

// Error: missing Username
// Error: missing NewPartNumber
// Error: missing Org
```

---

## Making API Changes Safely

### Non-Breaking Changes (Safe)

- Adding **optional** fields to the response
- Adding **optional** fields to the request
- Adding new endpoints
- Relaxing validation rules

### Breaking Changes (Dangerous)

- Removing or renaming response fields consumers depend on
- Adding **required** fields to the request that consumers don't send
- Changing field types (e.g., `string` → `int`)
- Changing HTTP status codes
- Changing the endpoint path

### Safe Change Process

1. Make your change locally
2. Run provider verification: `dotnet test` (provider tests)
3. If all consumer pacts pass → safe to merge
4. If any fail → coordinate with the affected consumer team

### Breaking Change Process

1. Announce the intended change to consumer teams
2. Agree on a migration timeline
3. Consumer teams update their contracts to include the new expectation
4. Consumer publishes updated pact to broker
5. Provider re-verifies → all pass
6. Deploy

---

## Using Can-I-Deploy

Before deploying, always check:

```bash
pact-broker can-i-deploy \
  --pacticipant="NIOP-Beat-Inventory-Api" \
  --version="your-version" \
  --to-environment="production" \
  --broker-base-url="http://localhost:9292"
```

If it returns **no**, do not deploy — a consumer contract is not satisfied.

---

## CI/CD Integration

The provider verification runs automatically via GitHub Actions (`provider-contract-verification.yml`):

1. **On push** to main/develop (provider code changes)
2. **On webhook** from Pact Broker (consumer pact changes)
3. **On manual trigger** (on-demand verification)

Pipeline jobs:
- **verify-provider** → runs contract tests against broker
- **can-i-deploy** → checks Pact Broker compatibility matrix
- **deploy-provider** → builds and deploys (main only)
- **record-deployment** → records the deployment in the broker
- **notify-failure** → generates failure summary on error

---

## FAQ

**Q: What if a consumer's contract seems wrong?**  
A: Contact the consumer team. The contract should reflect their actual API usage.

**Q: Can I add fields to the response without breaking consumers?**  
A: Yes. Adding new fields is always safe — consumers only assert on fields they define in their pacts.

**Q: What if I need to urgently deploy a fix?**  
A: You can still deploy if `can-i-deploy` passes. If it doesn't, assess the risk: the broken contract might be for a non-critical interaction.

**Q: How do I see which consumers are affected by a change?**  
A: Run provider verification and check which consumer pacts fail. The Pact Broker matrix also shows this.

**Q: Where do consumer tests live?**  
A: Consumer tests live in their own repositories. They publish pact files to the Pact Broker, which this repository verifies against.
