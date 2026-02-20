# NIOP Contract Testing with Pact .NET

> **Enterprise-Grade Consumer-Driven Contract Testing** for the NIOP Beat Inventory API  
> Protecting 8 consuming systems from breaking API changes

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Problem Statement](#problem-statement)
4. [Solution Design](#solution-design)
5. [Project Structure](#project-structure)
6. [Quick Start](#quick-start)
7. [API Under Test](#api-under-test)
8. [Consumer Systems](#consumer-systems)
9. [Running Tests Locally](#running-tests-locally)
10. [Pact Broker Setup](#pact-broker-setup)
11. [GitHub Actions CI/CD](#github-actions-cicd)
12. [How Contract Testing Works](#how-contract-testing-works)
13. [Adding New Consumers](#adding-new-consumers)
14. [Adding New API Endpoints](#adding-new-api-endpoints)
15. [Troubleshooting](#troubleshooting)
16. [Best Practices](#best-practices)
17. [Glossary](#glossary)

---

## Overview

This repository implements **Consumer-Driven Contract Testing (CDCT)** using **Pact .NET** for the NIOP (Beat Inventory) API. The `/api/UpdateDeviceInformation` endpoint is consumed by **8 different systems**, and changes to this API can have cascading impacts across the organization.

### What Does This Solve?

When the NIOP team changes the `UpdateDeviceInformation` API (e.g., adding/removing fields, changing response structure), **we need to know BEFORE deployment** if any consuming system will break.

### Key Numbers

| Metric | Value |
|--------|-------|
| Provider APIs | 1 (Beat.Inventory.Client.Api) |
| Endpoint Under Test | `/api/UpdateDeviceInformation` |
| Consumer Systems | 8 (Salesforce, PCAW, Soraian, MSA, INR, ATS, Cardiologs, EMR) |
| Total Contract Tests | 19 tests across all consumers |
| CI/CD Pipelines | 3 GitHub Actions workflows |

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        PACT BROKER                                  │
│                  (Central Contract Registry)                        │
│         ┌──────────────────────────────────┐                       │
│         │   Consumer Pacts (Contracts)      │                       │
│         │   Provider Verification Results   │                       │
│         │   Can-I-Deploy Matrix             │                       │
│         └──────────────────────────────────┘                       │
└──────────▲──────────────────────────────────────▲──────────────────┘
           │ Publish Pacts                        │ Verify & Report
           │                                      │
┌──────────┴──────────────┐         ┌────────────┴───────────────────┐
│   CONSUMER SIDE          │         │   PROVIDER SIDE                │
│   (8 Consuming Systems)  │         │   (NIOP Team)                  │
│                          │         │                                │
│  ┌─────────────────┐    │         │  ┌──────────────────────────┐  │
│  │ Salesforce Tests │    │         │  │ Beat.Inventory.Client.Api│  │
│  │ PCAW Tests       │    │         │  │                          │  │
│  │ Soraian Tests    │    │         │  │  POST /api/Update        │  │
│  │ MSA Tests        │    │         │  │  DeviceInformation       │  │
│  │ INR Tests        │    │         │  └──────────────────────────┘  │
│  │ ATS Tests        │    │         │                                │
│  │ Cardiologs Tests │    │         │  ┌──────────────────────────┐  │
│  │ EMR Tests        │    │         │  │ Provider Verification    │  │
│  └─────────────────┘    │         │  │ Tests                    │  │
│                          │         │  └──────────────────────────┘  │
│  Output: Pact JSON files │         │  Output: Verification results  │
└──────────────────────────┘         └────────────────────────────────┘
```

---

## Problem Statement

The NIOP team maintains the `Beat.Inventory.Client.Api` which exposes the `POST /api/UpdateDeviceInformation` endpoint. This endpoint is **directly impacted by part number changes** and is consumed by 8 different systems:

| System | Usage | Impact Level |
|--------|-------|-------------|
| **Salesforce** | Field service device replacements | High |
| **PCAW** | Patient care workflow device updates | Critical |
| **Soraian** | Inventory reconciliation & tracking | High |
| **MSA** | Mobile field operations & device swap | High |
| **INR** | INR monitoring device calibration | Critical |
| **ATS** | Automated testing pipelines | Medium |
| **Cardiologs** | Cardiac device analytics mapping | Critical |
| **EMR** | Patient record compliance sync | Critical |

**Without contract testing**, any change to the API structure could silently break multiple systems, discovered only in integration or production environments.

---

## Solution Design

### Consumer-Driven Contract Testing (CDCT)

```
Traditional Integration Testing:
  Consumer → [Network] → Provider → [Database]
  ❌ Slow, flaky, hard to maintain, requires all systems running

Contract Testing with Pact:
  Step 1: Consumer defines expectations (Pact file)
  Step 2: Provider verifies it can meet those expectations
  ✅ Fast, reliable, independent, no network required
```

### Technology Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| Contract Testing | PactNet | 5.0.0 |
| Test Framework | xUnit | 2.6.2 |
| API Framework | ASP.NET Core | 8.0 |
| Assertions | FluentAssertions | 6.12.0 |
| Mocking | Moq | 4.20.70 |
| Pact Broker | Docker (pactfoundation/pact-broker) | Latest |
| Database (Broker) | PostgreSQL 16 | Alpine |
| CI/CD | GitHub Actions | v4 |

---

## Project Structure

```
NIOP_PartNumberEndpoints/
│
├── 📄 NIOP.ContractTesting.sln          # Solution file
├── 📄 README.md                          # This documentation
├── 📄 .gitignore                         # Git ignore rules
│
├── 📁 src/
│   ├── 📁 Provider/                      # NIOP Team (API Owner)
│   │   ├── 📁 NIOP.Provider.Api/         # The actual API
│   │   │   ├── Controllers/
│   │   │   │   └── DeviceController.cs   # UpdateDeviceInformation endpoint
│   │   │   ├── Services/
│   │   │   │   ├── IDeviceService.cs
│   │   │   │   └── DeviceService.cs
│   │   │   └── Program.cs
│   │   │
│   │   └── 📁 NIOP.Provider.ContractTests/  # Provider verification tests
│   │       ├── Fixtures/
│   │       │   └── ProviderWebApplicationFactory.cs
│   │       └── ProviderContractTests.cs
│   │
│   ├── 📁 Shared/                        # Shared contracts & models
│   │   └── 📁 NIOP.Contracts.Shared/
│   │       ├── Models/
│   │       │   ├── UpdateDeviceInformationRequest.cs
│   │       │   └── UpdateDeviceInformationResponse.cs
│   │       ├── Constants/
│   │       │   └── PactConstants.cs
│   │       └── Client/
│   │           └── NiopInventoryApiClient.cs
│   │
│   └── 📁 Consumers/                    # Consumer Teams
│       ├── 📁 Salesforce/
│       │   └── Consumer.Salesforce.ContractTests/
│       ├── 📁 PCAW/
│       │   └── Consumer.PCAW.ContractTests/
│       ├── 📁 Soraian/
│       │   └── Consumer.Soraian.ContractTests/
│       ├── 📁 MSA/
│       │   └── Consumer.MSA.ContractTests/
│       ├── 📁 INR/
│       │   └── Consumer.INR.ContractTests/
│       ├── 📁 ATS/
│       │   └── Consumer.ATS.ContractTests/
│       ├── 📁 Cardiologs/
│       │   └── Consumer.Cardiologs.ContractTests/
│       └── 📁 EMR/
│           └── Consumer.EMR.ContractTests/
│
├── 📁 pacts/                             # Generated Pact JSON files
│
├── 📁 infrastructure/                    # Infrastructure setup
│   ├── docker-compose.pact-broker.yml    # Pact Broker + PostgreSQL
│   ├── setup-pact-broker.sh             # Linux/Mac setup script
│   └── setup-pact-broker.ps1            # Windows setup script
│
├── 📁 .github/workflows/                # CI/CD Pipelines
│   ├── consumer-contract-tests.yml       # Consumer test pipeline
│   ├── provider-contract-verification.yml # Provider verification pipeline
│   └── pact-broker-setup.yml            # Webhook configuration
│
└── 📁 docs/                             # Additional documentation
    ├── CONSUMER_GUIDE.md                 # Guide for consumer teams
    ├── PROVIDER_GUIDE.md                 # Guide for NIOP team
    └── PACT_BROKER_GUIDE.md             # Pact Broker operations guide
```

---

## Quick Start

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for Pact Broker)
- Git

### Step 1: Clone and Build

```bash
git clone <repository-url>
cd NIOP_PartNumberEndpoints
dotnet restore NIOP.ContractTesting.sln
dotnet build NIOP.ContractTesting.sln
```

### Step 2: Run Consumer Tests (Generate Pacts)

```bash
# Run ALL consumer tests
dotnet test NIOP.ContractTesting.sln --filter "FullyQualifiedName~Consumer" --configuration Release

# Or run a specific consumer
dotnet test src/Consumers/Salesforce/Consumer.Salesforce.ContractTests/Consumer.Salesforce.ContractTests.csproj
```

### Step 3: Start Pact Broker

```powershell
# Windows
.\infrastructure\setup-pact-broker.ps1

# Linux/Mac
chmod +x infrastructure/setup-pact-broker.sh
./infrastructure/setup-pact-broker.sh
```

### Step 4: Run Provider Verification

```bash
dotnet test src/Provider/NIOP.Provider.ContractTests/NIOP.Provider.ContractTests.csproj --configuration Release
```

### Step 5: View Results

Open http://localhost:9292 in your browser to see the Pact Broker UI with all contracts and verification results.

---

## API Under Test

### `POST /api/UpdateDeviceInformation`

**Service:** Beat.Inventory.Client.Api  
**Impact:** Affected by new PartNumber changes  
**Headers:** `Content-Type: application/json`, `CORS: *`

#### Request Body

```json
{
  "SerialNumber": "SN-2024-001234",
  "NewPartNumber": "PN-BEAT-5678-REV2",
  "Username": "system.integration"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `SerialNumber` | string | Yes | Device serial number to update |
| `NewPartNumber` | string | Yes | New part number to assign (impacted by NIOP changes) |
| `Username` | string | Yes | User/system performing the update |

#### Success Response (200 OK)

```json
{
  "Success": true,
  "Message": "Device information updated successfully.",
  "CorrelationId": "guid-correlation-id"
}
```

#### Error Response (400 Bad Request)

```json
{
  "Success": false,
  "Message": "Serial number is required.",
  "CorrelationId": "guid-correlation-id"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `Success` | boolean | Whether the update succeeded |
| `Message` | string | Human-readable result description |
| `CorrelationId` | string | Request tracking ID for distributed tracing |

---

## Consumer Systems

### Test Scenarios Per Consumer

| Consumer | Tests | Scenarios |
|----------|-------|-----------|
| **Salesforce** | 2 | Success update, Missing serial number |
| **PCAW** | 2 | Patient workflow update, Empty part number |
| **Soraian** | 2 | Inventory reconciliation, Correlation ID validation |
| **MSA** | 2 | Field operation update, Missing username |
| **INR** | 2 | INR device update, Boolean Success validation |
| **ATS** | 2 | Testing pipeline update, Batch processing |
| **Cardiologs** | 2 | Cardiac device update, Message field validation |
| **EMR** | 3 | Patient record sync, Compliance validation, Error structure |

### Why Different Test Scenarios?

Each consumer uses the API differently and has different requirements:
- **EMR** needs ALL response fields for regulatory compliance
- **Soraian** requires CorrelationId for distributed tracing
- **Cardiologs** requires Message field for diagnostics logging
- **INR** validates boolean Success for clear clinical decisions
- **MSA** can have missing username in field conditions
- **PCAW** may send empty part numbers during workflow errors

---

## Running Tests Locally

### Run All Tests

```bash
dotnet test NIOP.ContractTesting.sln --configuration Release --verbosity normal
```

### Run Specific Consumer Tests

```bash
# Salesforce
dotnet test src/Consumers/Salesforce/Consumer.Salesforce.ContractTests/Consumer.Salesforce.ContractTests.csproj

# EMR
dotnet test src/Consumers/EMR/Consumer.EMR.ContractTests/Consumer.EMR.ContractTests.csproj

# Any other consumer - follow the same pattern
dotnet test src/Consumers/{ConsumerName}/Consumer.{ConsumerName}.ContractTests/Consumer.{ConsumerName}.ContractTests.csproj
```

### Run Provider Verification Against Local Pacts

```bash
# First generate consumer pacts
dotnet test NIOP.ContractTesting.sln --filter "FullyQualifiedName~Consumer"

# Then verify provider
dotnet test src/Provider/NIOP.Provider.ContractTests/NIOP.Provider.ContractTests.csproj
```

### Run Provider Verification Against Pact Broker

```bash
# Set environment variables
$env:PACT_BROKER_BASE_URL = "http://localhost:9292"
$env:PACT_BROKER_TOKEN = ""  # Or use basic auth
$env:PROVIDER_VERSION = "1.0.0-local"

dotnet test src/Provider/NIOP.Provider.ContractTests/NIOP.Provider.ContractTests.csproj
```

---

## Pact Broker Setup

### Local Development (Docker)

The Pact Broker is deployed using Docker Compose with PostgreSQL as the backend database.

#### Start

```powershell
# Windows
.\infrastructure\setup-pact-broker.ps1

# Or manually
docker-compose -f infrastructure/docker-compose.pact-broker.yml up -d
```

#### Access

| Service | URL | Credentials |
|---------|-----|-------------|
| Pact Broker UI | http://localhost:9292 | pact_user / pact_password |
| PostgreSQL | localhost:5432 | pact_broker / pact_broker_password |

#### Stop

```bash
docker-compose -f infrastructure/docker-compose.pact-broker.yml down
```

#### Reset (Delete All Data)

```bash
docker-compose -f infrastructure/docker-compose.pact-broker.yml down -v
```

### Production Pact Broker

For production use, consider:

1. **PactFlow** (SaaS) - Managed Pact Broker at https://pactflow.io
2. **Self-hosted** - Deploy the Docker Compose setup to your infrastructure
3. **Kubernetes** - Use Helm charts for Pact Broker deployment

#### GitHub Secrets Required

| Secret | Description | Example |
|--------|-------------|---------|
| `PACT_BROKER_BASE_URL` | Pact Broker URL | `https://your-org.pactflow.io` |
| `PACT_BROKER_TOKEN` | API token for authentication | `abc123...` |

---

## GitHub Actions CI/CD

### Workflow Overview

```
Consumer Code Change          Provider Code Change
        │                              │
        ▼                              ▼
┌─────────────────┐          ┌──────────────────────┐
│ consumer-contract│          │ provider-contract-    │
│ -tests.yml      │          │ verification.yml      │
│                 │          │                       │
│ 1. Build        │          │ 1. Build              │
│ 2. Run 8 tests  │──pacts──▶│ 2. Verify all pacts   │
│    (parallel)   │          │ 3. can-i-deploy       │
│ 3. Publish pacts│          │ 4. Record release     │
│ 4. can-i-deploy │          │                       │
└─────────────────┘          └──────────────────────┘
                                      │
                              ┌───────┴────────┐
                              │ Pact Broker     │
                              │ Webhook         │
                              │ (on new pact)   │
                              └────────────────┘
```

### 1. Consumer Contract Tests (`consumer-contract-tests.yml`)

**Triggers:** Push to main/develop, PRs, manual  
**What it does:**
- Builds the solution
- Runs all 8 consumer tests **in parallel**
- Uploads test results and generated pact files
- Publishes pacts to Pact Broker (main/develop only)
- Runs `can-i-deploy` safety check

### 2. Provider Contract Verification (`provider-contract-verification.yml`)

**Triggers:** Push to main/develop, PRs, Pact Broker webhook, manual  
**What it does:**
- Builds the provider
- Verifies ALL consumer pacts from the Broker
- Publishes verification results
- Runs `can-i-deploy` and records release (main only)
- Notifies on failure with impacted systems list

### 3. Pact Broker Setup (`pact-broker-setup.yml`)

**Triggers:** Manual only  
**What it does:**
- Creates webhooks for automated provider verification
- Registers pacticipants (consumers + provider)
- Creates environments (development, staging, production)

### Setting Up GitHub Actions

1. Go to your GitHub repository → Settings → Secrets and variables → Actions
2. Add the following secrets:

```
PACT_BROKER_BASE_URL = https://your-broker-url.com
PACT_BROKER_TOKEN    = your-api-token
```

3. Run the `Setup Pact Broker Webhooks` workflow with "create-webhook" action
4. Push code to trigger the consumer and provider workflows

---

## How Contract Testing Works

### The Pact Workflow (Step by Step)

```
╔═══════════════════════════════════════════════════════╗
║  STEP 1: Consumer Defines Expectations (Pact File)    ║
╠═══════════════════════════════════════════════════════╣
║                                                       ║
║  Consumer test says:                                  ║
║  "When I send POST /api/UpdateDeviceInformation       ║
║   with { SerialNumber: 'SN-001', ... }                ║
║   I expect 200 OK with { Success: true, ... }"        ║
║                                                       ║
║  Output: Pact JSON file (the contract)                ║
╚═══════════════════════════════════════════════════════╝
                         │
                         ▼
╔═══════════════════════════════════════════════════════╗
║  STEP 2: Pact Published to Broker                     ║
╠═══════════════════════════════════════════════════════╣
║                                                       ║
║  Pact file uploaded to central Pact Broker:           ║
║  - Consumer name & version recorded                   ║
║  - Git branch tagged                                  ║
║  - Webhook triggers provider verification             ║
║                                                       ║
╚═══════════════════════════════════════════════════════╝
                         │
                         ▼
╔═══════════════════════════════════════════════════════╗
║  STEP 3: Provider Verifies the Pact                   ║
╠═══════════════════════════════════════════════════════╣
║                                                       ║
║  Provider test:                                       ║
║  1. Starts the actual API                             ║
║  2. Replays each consumer interaction                 ║
║  3. Compares actual response vs expected              ║
║  4. Reports pass/fail to Pact Broker                  ║
║                                                       ║
║  If FAIL → NIOP team knows their change breaks X      ║
║  If PASS → Safe to deploy                             ║
╚═══════════════════════════════════════════════════════╝
                         │
                         ▼
╔═══════════════════════════════════════════════════════╗
║  STEP 4: Can-I-Deploy Safety Check                    ║
╠═══════════════════════════════════════════════════════╣
║                                                       ║
║  Before deploying, ask the Pact Broker:               ║
║  "Can this version be safely deployed to prod?"       ║
║                                                       ║
║  Broker checks ALL consumer/provider combinations     ║
║  and returns ✅ SAFE or ❌ BLOCKED                     ║
╚═══════════════════════════════════════════════════════╝
```

### What Happens When NIOP Changes the API?

**Scenario:** NIOP adds a required `DeviceType` field to the request.

1. NIOP developer makes the change locally
2. Runs provider verification → **FAILS** for all 8 consumers
3. `can-i-deploy` → **BLOCKED** (consumers don't know about DeviceType yet)
4. NIOP coordinates with consumer teams
5. Consumer teams update their tests and pacts
6. Provider re-verifies → **PASSES**
7. `can-i-deploy` → **SAFE**
8. Both sides deploy

---

## Adding New Consumers

To add a new consuming system (e.g., "NewSystem"):

### 1. Create the project structure

```
src/Consumers/NewSystem/Consumer.NewSystem.ContractTests/
├── Consumer.NewSystem.ContractTests.csproj
└── NewSystemUpdateDeviceTests.cs
```

### 2. Create the .csproj file

Copy from any existing consumer and update the `RootNamespace`.

### 3. Create the test class

```csharp
using NIOP.Contracts.Shared.Constants;
using PactNet;

public class NewSystemUpdateDeviceTests
{
    private readonly IPactBuilderV4 _pactBuilder;

    public NewSystemUpdateDeviceTests(ITestOutputHelper output)
    {
        var pact = Pact.V4("NewSystem-Consumer", PactConstants.ProviderName, new PactConfig
        {
            PactDir = Path.Combine("..", "..", "..", "..", "..", "..", "pacts")
        });
        _pactBuilder = pact.WithHttpInteractions();
    }

    // Add your test methods...
}
```

### 4. Register in PactConstants

Add the consumer name in `PactConstants.Consumers`:
```csharp
public const string NewSystem = "NewSystem-Consumer";
```

### 5. Update GitHub Actions

Add to the consumer matrix in `consumer-contract-tests.yml`:
```yaml
- { name: "NewSystem", project: "src/Consumers/NewSystem/..." }
```

### 6. Add to the provider InlineData

Update `ProviderContractTests.cs`:
```csharp
[InlineData(PactConstants.Consumers.NewSystem)]
```

---

## Adding New API Endpoints

To add contract tests for additional NIOP endpoints:

### 1. Add models to Shared project

Create request/response models in `NIOP.Contracts.Shared/Models/`.

### 2. Update PactConstants

Add the endpoint path in `PactConstants.Endpoints`.

### 3. Add to api client

Update `NiopInventoryApiClient.cs` with the new method.

### 4. Add consumer tests

Each consumer that uses the endpoint should add test methods.

### 5. Add provider endpoint

Ensure the endpoint exists in `DeviceController.cs`.

---

## Troubleshooting

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| Pact file not generated | Test didn't run successfully | Check test output for errors |
| Provider verification fails | API response doesn't match pact | Compare actual vs expected in test output |
| Pact Broker connection refused | Docker not running | Start Docker Desktop, run setup script |
| Port 9292 in use | Another service using the port | Stop the conflicting service or change port |
| `can-i-deploy` fails | No verified pact for version | Run both consumer and provider tests first |
| Tests timeout | Mock server port conflict | Check if ports are available |

### Debugging Provider Verification

```bash
# Run with detailed logging
dotnet test src/Provider/NIOP.Provider.ContractTests/NIOP.Provider.ContractTests.csproj --verbosity detailed
```

### Viewing Pact File Contents

```bash
# Pretty-print a pact file
cat pacts/Salesforce-Consumer-NIOP-Beat-Inventory-Api.json | python -m json.tool
```

### Resetting Pact Broker

```bash
# Remove all data and restart
docker-compose -f infrastructure/docker-compose.pact-broker.yml down -v
docker-compose -f infrastructure/docker-compose.pact-broker.yml up -d
```

---

## Best Practices

### For Consumer Teams

1. **Test what you USE, not everything** - Only include fields your system actually reads
2. **Use provider states** - Describe the preconditions your test needs
3. **Don't test business logic** - Contract tests verify structure, not behavior
4. **Version your pacts** - Use git commit SHA as the consumer version in CI
5. **Run tests on every PR** - Catch contract issues early

### For the Provider Team (NIOP)

1. **Run provider verification on every PR** - Before merging any API changes
2. **Use `can-i-deploy`** - Never deploy without checking the Pact Broker
3. **Set up webhooks** - Automatically verify when consumers publish new pacts
4. **Use provider states** - Set up test data for each consumer scenario
5. **Communicate changes** - Use Pact Broker comments/tags to coordinate

### General

1. **Keep contracts minimal** - Only assert on fields you need
2. **Use meaningful descriptions** - Help future developers understand the intent
3. **Tag releases** - Use `record-release` to track what's deployed
4. **Review the Pact Broker regularly** - Use the network diagram to see dependencies
5. **Don't share pact files via email** - Always use the Pact Broker

---

## Glossary

| Term | Definition |
|------|-----------|
| **Pact** | A contract (JSON file) between a consumer and provider that defines expected interactions |
| **Consumer** | A system that calls an API (e.g., Salesforce calling NIOP) |
| **Provider** | A system that exposes an API (e.g., NIOP Beat Inventory API) |
| **Pact Broker** | A central server that stores pacts and verification results |
| **Provider State** | A description of the provider's precondition for a specific interaction |
| **Verification** | The process of replaying consumer interactions against the real provider |
| **can-i-deploy** | A Pact Broker CLI command that checks if a version is safe to deploy |
| **Pacticipant** | A participant in a pact (either consumer or provider) |
| **Webhook** | An HTTP callback triggered by the Pact Broker when events occur |
| **Consumer Version Selector** | Rules for which consumer pact versions to verify against |
| **Pending Pacts** | New pacts that haven't been verified yet (won't fail the build) |
| **WIP Pacts** | Work-in-progress pacts from feature branches |

---

## License

Internal use only - NIOP Team / [Your Organization]

---

*Documentation generated for the NIOP Contract Testing initiative. For questions, contact the NIOP team or refer to the [Pact documentation](https://docs.pact.io/).*
