# Consumer Team Guide

> How to write and maintain Pact consumer tests for the NIOP Beat Inventory API

---

## Who Is This For?

This guide is for developers on consumer teams (**Salesforce, PCAW, Soraian, MSA, INR, ATS, Cardiologs, EMR**) who need to write or update contract tests against the NIOP API.

---

## What You Need to Do

As a consumer team, your responsibility is to:

1. **Define what you expect** from the NIOP API (the contract)
2. **Keep your contract up to date** when your usage changes
3. **Respond to contract verification failures** when the NIOP team makes breaking changes

---

## Writing a Consumer Test

### Step 1: Understand the Pattern

Every consumer test follows this structure:

```csharp
[Fact]
public async Task TestName()
{
    // 1. ARRANGE - Define what you'll send and what you expect back
    _pactBuilder
        .UponReceiving("a human-readable description of this interaction")
        .Given("a provider state describing preconditions")
        .WithRequest(HttpMethod.Post, "/api/UpdateDeviceInformation")
        .WithHeader("Content-Type", "application/json")
        .WithJsonBody(new { /* your request */ })
        .WillRespond()
        .WithStatus(HttpStatusCode.OK)
        .WithJsonBody(new { /* expected response */ });

    // 2. ACT & ASSERT - Make the call and verify
    await _pactBuilder.VerifyAsync(async ctx =>
    {
        var client = new HttpClient { BaseAddress = ctx.MockServerUri };
        var response = await client.PostAsJsonAsync("/api/UpdateDeviceInformation", request);
        
        // Assert what matters to YOUR system
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UpdateDeviceInformationResponse>();
        result!.Success.Should().BeTrue();
    });
}
```

### Step 2: Only Assert What You Use

**Critical Rule:** Your contract should only include fields your system actually reads.

```csharp
// ✅ GOOD - Only asserting fields your system uses
.WithJsonBody(new { Success = true })

// ❌ BAD - Asserting fields you don't use creates unnecessary coupling
.WithJsonBody(new { Success = true, Message = "exact text", CorrelationId = "specific-id" })
```

### Step 3: Use Meaningful Descriptions

```csharp
// ✅ GOOD
.UponReceiving("a request from PCAW to update device part number during patient care workflow")
.Given("a device with serial number SN-PCAW-2024-100 exists in inventory")

// ❌ BAD
.UponReceiving("test 1")
.Given("state 1")
```

### Step 4: Cover Your Error Cases

Test what happens when things go wrong:

```csharp
// Test missing required fields
_pactBuilder
    .UponReceiving("a request with missing serial number")
    .Given("an update request with empty serial number")
    .WithRequest(HttpMethod.Post, "/api/UpdateDeviceInformation")
    .WithJsonBody(new { SerialNumber = "", NewPartNumber = "PN-001", Username = "user" })
    .WillRespond()
    .WithStatus(HttpStatusCode.BadRequest)
    .WithJsonBody(new { Success = false, Message = "Serial number is required." });
```

---

## Running Your Tests

```bash
# Navigate to your consumer test project
cd src/Consumers/YourSystem/Consumer.YourSystem.ContractTests

# Run tests
dotnet test --configuration Release --verbosity normal
```

After running, check the `pacts/` directory for your generated pact file:
```
pacts/YourSystem-Consumer-NIOP-Beat-Inventory-Api.json
```

---

## What Happens After You Publish

1. Your pact is published to the Pact Broker
2. The Pact Broker triggers the NIOP provider verification
3. NIOP's tests replay your interactions against their actual API
4. Results are posted back to the Pact Broker
5. You can check `can-i-deploy` to see if you're safe to deploy

---

## When the Provider Changes

If the NIOP team changes the API and your contract verification fails:

1. You'll see the failure in the Pact Broker UI
2. Review what changed (new fields, removed fields, type changes)
3. Update your consumer tests to match the new contract
4. Run tests and publish the updated pact
5. Verify with the provider team that everything passes

---

## FAQ

**Q: Do I need Docker to run consumer tests?**  
A: No. Consumer tests use a Pact mock server that runs in-process. Docker is only needed for the Pact Broker.

**Q: Can I run tests offline?**  
A: Yes. Consumer tests generate local pact files. Publishing to the Broker requires network access.

**Q: What if I add a new use of the API?**  
A: Add a new test method that defines the new interaction. Run tests to generate an updated pact.

**Q: How often should I update my contracts?**  
A: Whenever your usage of the API changes. Also review when the NIOP team announces breaking changes.
