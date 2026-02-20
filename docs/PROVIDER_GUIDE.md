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
│    a. Set provider state │
│    b. Replay request     │
│    c. Compare response   │
│ 4. Report results to     │
│    Pact Broker           │
└──────────────────────────┘
```

### Running Verification Locally

```bash
# Against local pact files (after running consumer tests)
dotnet test src/Provider/NIOP.Provider.ContractTests/NIOP.Provider.ContractTests.csproj

# Against Pact Broker
$env:PACT_BROKER_BASE_URL = "http://localhost:9292"
$env:PROVIDER_VERSION = "local-dev"
dotnet test src/Provider/NIOP.Provider.ContractTests/NIOP.Provider.ContractTests.csproj
```

---

## Managing Provider States

Provider states set up preconditions for consumer interactions.

### What Are Provider States?

When a consumer says:
```
Given "a device with serial number SN-SF-2024-001 exists"
```

Your provider test must ensure that state is true before running the interaction.

### Current Implementation

In `ProviderWebApplicationFactory.cs`, the mock `DeviceService` is set up to handle all provider states:

```csharp
// Default: successful update
MockDeviceService
    .Setup(s => s.UpdateDeviceInformationAsync(It.IsAny<UpdateDeviceInformationRequest>()))
    .ReturnsAsync(new UpdateDeviceInformationResponse { Success = true, ... });

// State: empty serial number → error
MockDeviceService
    .Setup(s => s.UpdateDeviceInformationAsync(
        It.Is<UpdateDeviceInformationRequest>(r => string.IsNullOrWhiteSpace(r.SerialNumber))))
    .ReturnsAsync(new UpdateDeviceInformationResponse { Success = false, ... });
```

### Adding New Provider States

When consumers define new states, update the factory:

```csharp
// New state: "a device with serial number X does not exist"
MockDeviceService
    .Setup(s => s.UpdateDeviceInformationAsync(
        It.Is<UpdateDeviceInformationRequest>(r => r.SerialNumber == "NON-EXISTENT")))
    .ThrowsAsync(new KeyNotFoundException("Device not found"));
```

---

## Making API Changes Safely

### Non-Breaking Changes (Safe)

These changes won't break consumers:
- Adding **optional** fields to the response
- Adding **optional** fields to the request
- Adding new endpoints
- Relaxing validation rules

### Breaking Changes (Dangerous)

These WILL break consumers — coordinate first:
- Removing or renaming response fields
- Adding **required** fields to the request
- Changing field types (string → int)
- Changing HTTP status codes
- Changing the endpoint path

### Safe Change Process

1. Make your change locally
2. Run provider verification: `dotnet test` (provider tests)
3. If all pass → safe to merge
4. If any fail → coordinate with affected consumers

### Breaking Change Process

1. Announce the change to all consumer teams
2. Agree on a migration timeline
3. Consider versioning (`/api/v2/UpdateDeviceInformation`)
4. Consumer teams update their pacts
5. Verify all updated pacts pass
6. Deploy together or use feature flags

---

## Using Can-I-Deploy

Before deploying to any environment, always check:

```bash
pact-broker can-i-deploy \
  --pacticipant="NIOP-Beat-Inventory-Api" \
  --version="your-version" \
  --to-environment="production" \
  --broker-base-url="http://localhost:9292"
```

If it returns ❌, **DO NOT DEPLOY** — a consumer contract is not satisfied.

---

## Monitoring the Pact Broker

### Key Pages to Watch

| Page | URL | Purpose |
|------|-----|---------|
| Dashboard | http://localhost:9292 | Overview of all pacts |
| Network Diagram | http://localhost:9292/groups | Visual dependency map |
| Matrix | http://localhost:9292/matrix | Compatibility matrix |

### Webhook Notifications

The Pact Broker webhook triggers your provider verification workflow whenever a consumer publishes a new pact. This means you'll know immediately if a consumer changes their expectations.

---

## CI/CD Integration

The provider verification runs automatically via GitHub Actions:

1. **On push** to main/develop (provider code changes)
2. **On webhook** from Pact Broker (consumer pact changes)
3. **On manual trigger** (on-demand verification)

Results are:
- Published to the Pact Broker
- Available as GitHub Actions artifacts
- Visible in the GitHub Actions run summary

---

## FAQ

**Q: What if a consumer's test seems wrong?**  
A: Contact the consumer team. The contract should reflect their actual usage. If they assert on fields they don't use, that's a consumer-side issue.

**Q: Can I add fields to the response without breaking consumers?**  
A: Yes, adding new optional fields is always safe. Consumers only assert on fields they define in their pacts.

**Q: What if I need to urgently deploy a fix?**  
A: You can still deploy if `can-i-deploy` passes. If it doesn't, assess the risk: the broken contract might be for a non-critical consumer or an edge case.

**Q: How do I see which consumers are affected by a change?**  
A: Run provider verification and check which consumer tests fail. The Pact Broker matrix also shows this clearly.
