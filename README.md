# NIOP Provider Contract Testing with Pact .NET

> **Provider-Side Contract Verification** for the NIOP Beat Inventory API  
> Ensuring consumer contracts are honoured before deployment

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Project Structure](#project-structure)
4. [Quick Start](#quick-start)
5. [API Under Test](#api-under-test)
6. [Running Tests Locally](#running-tests-locally)
7. [Pact Broker Setup](#pact-broker-setup)
8. [GitHub Actions CI/CD](#github-actions-cicd)
9. [How Contract Testing Works](#how-contract-testing-works)
10. [Troubleshooting](#troubleshooting)
11. [Best Practices](#best-practices)
12. [Glossary](#glossary)

---

## Overview

This repository contains the **provider-side** implementation of Consumer-Driven Contract Testing (CDCT) using **Pact .NET** for the NIOP Beat Inventory API. The `POST /api/UpdateDeviceInformation` endpoint is consumed by external systems, and changes to this API must be validated against their published contracts before deployment.

Consumer tests live in their own repositories and publish pacts to the Pact Broker. This repository fetches those pacts and verifies the provider can satisfy them.

### Key Numbers

| Metric | Value |
|--------|-------|
| Provider API | Beat.Inventory.Client.Api |
| Endpoint Under Test | `POST /api/UpdateDeviceInformation` |
| Framework | .NET 10.0, PactNet 5.0.0, xUnit 2.6.2 |
| CI/CD Pipeline | 1 GitHub Actions workflow (5 jobs) |

---

## Architecture

```
┌───────────────────────────────────────────────────────────────────┐
│                        PACT BROKER                                │
│                  (Central Contract Registry)                      │
│         ┌──────────────────────────────────┐                     │
│         │   Consumer Pacts (Contracts)      │                     │
│         │   Provider Verification Results   │                     │
│         │   Can-I-Deploy Matrix             │                     │
│         └──────────────────────────────────┘                     │
└──────────▲──────────────────────────────────────▲────────────────┘
           │ Publish Pacts                        │ Verify & Report
           │                                      │
┌──────────┴──────────────┐         ┌────────────┴─────────────────┐
│   CONSUMER SIDE          │         │   PROVIDER SIDE (this repo)  │
│   (External Repos)       │         │                              │
│                          │         │  ┌────────────────────────┐  │
│  Consumers publish       │         │  │ NIOP.Provider.Api      │  │
│  pact JSON files to the  │         │  │                        │  │
│  Pact Broker describing  │         │  │  POST /api/Update      │  │
│  their API expectations. │         │  │  DeviceInformation     │  │
│                          │         │  └────────────────────────┘  │
│  e.g. PCAW-Consumer      │         │                              │
│                          │         │  ┌────────────────────────┐  │
│                          │         │  │ Provider Verification  │  │
│                          │         │  │ Tests                  │  │
│                          │         │  └────────────────────────┘  │
│                          │         │  Output: Verification results│
└──────────────────────────┘         └──────────────────────────────┘
```

---

## Project Structure

```
NIOP_PartNumberEndpoints/
│
├── NIOP.Provider.sln                     # Solution file (2 projects)
├── README.md                             # This documentation
│
├── src/Provider/
│   ├── NIOP.Provider.Api/                # The actual API
│   │   ├── Controllers/
│   │   │   └── DeviceController.cs       # UpdateDeviceInformation endpoint
│   │   ├── Models/
│   │   │   ├── UpdateDeviceInformationRequest.cs
│   │   │   └── UpdateDeviceInformationResponse.cs
│   │   ├── Services/
│   │   │   ├── IDeviceService.cs
│   │   │   └── DeviceService.cs
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   └── NIOP.Provider.ContractTests/      # Provider verification tests
│       ├── Constants/
│       │   └── PactConstants.cs          # Provider/consumer names, broker config
│       ├── Fixtures/
│       │   └── ProviderWebApplicationFactory.cs  # Real Kestrel host with mocked services
│       └── ProviderContractTests.cs      # Pact verification test methods
│
├── pacts/                                # Local pact JSON files (for dev/testing)
│   └── PCAW-Consumer-NIOP-Beat-Inventory-Api.json
│
├── infrastructure/                       # Pact Broker infrastructure
│   ├── docker-compose.pact-broker.yml    # Pact Broker + PostgreSQL
│   ├── setup-pact-broker.ps1             # Windows setup script
│   └── setup-pact-broker.sh              # Linux/macOS setup script
│
├── .github/workflows/
│   └── provider-contract-verification.yml  # CI/CD pipeline
│
└── docs/
    ├── PROVIDER_GUIDE.md                 # Guide for provider team
    └── PACT_BROKER_GUIDE.md              # Pact Broker operations guide
```

---

## Quick Start

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for Pact Broker)
- Git

### Step 1: Clone and Build

```bash
git clone <repository-url>
cd NIOP_PartNumberEndpoints
dotnet restore NIOP.Provider.sln
dotnet build NIOP.Provider.sln
```

### Step 2: Run Provider Verification (Local Pacts)

```bash
dotnet test src/Provider/NIOP.Provider.ContractTests/NIOP.Provider.ContractTests.csproj --configuration Release
```

This verifies the provider against pact files in the `pacts/` directory.

### Step 3: Run Provider Verification (Pact Broker)

```powershell
$env:PACT_BROKER_BASE_URL = "http://localhost:9292"
$env:PACT_BROKER_USERNAME = "pact_user"
$env:PACT_BROKER_PASSWORD = "pact_password"
$env:PROVIDER_VERSION = "1.0.0-local"

dotnet test src/Provider/NIOP.Provider.ContractTests/NIOP.Provider.ContractTests.csproj --configuration Release
```

### Step 4: View Results

Open http://localhost:9292 in your browser to see the Pact Broker dashboard.

---

## API Under Test

### `POST /api/UpdateDeviceInformation`

**Service:** Beat.Inventory.Client.Api  
**Impact:** Affected by NIOP part number changes

#### Request Body

```json
{
  "SerialNumber": "SN-2024-001234",
  "NewPartNumber": "PN-BEAT-5678-REV2",
  "Username": "system.integration",
  "Org": "philips"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `SerialNumber` | string | Yes | Device serial number to update |
| `NewPartNumber` | string | Yes | New part number to assign (impacted by NIOP changes) |
| `Username` | string | Yes | User/system performing the update |
| `Org` | string | Yes | Organization associated with the update |

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

## Running Tests Locally

### Run All Provider Tests

```bash
dotnet test src/Provider/NIOP.Provider.ContractTests/NIOP.Provider.ContractTests.csproj --configuration Release --verbosity normal
```

### Run Only Pact Broker Verification

```bash
dotnet test src/Provider/NIOP.Provider.ContractTests/NIOP.Provider.ContractTests.csproj --filter "DisplayName~Pact Broker"
```

### Run Only Local Pact File Verification

```bash
dotnet test src/Provider/NIOP.Provider.ContractTests/NIOP.Provider.ContractTests.csproj --filter "DisplayName~individual"
```

---

## Pact Broker Setup

### Local Development (Docker)

#### Start

```powershell
# Windows
.\infrastructure\setup-pact-broker.ps1

# Linux/macOS
chmod +x infrastructure/setup-pact-broker.sh
./infrastructure/setup-pact-broker.sh

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

See [docs/PACT_BROKER_GUIDE.md](docs/PACT_BROKER_GUIDE.md) for full broker operations.

For testing purpose, i have not hosted a entire pact_broker setup, i have re-used the available PACT_BROKER from PACTFLOW
https://docs.pact.io/university/introduction/step13
---

## GitHub Actions CI/CD

### Workflow: `provider-contract-verification.yml`

**Triggers:**
- Push/PR to `main` or `develop` (provider code changes)
- Pact Broker webhook (`repository_dispatch: pact-changed`)
- Manual dispatch

### Pipeline Flow

```
┌────────────────────────┐
│  verify-provider       │  Build + run provider contract tests
│  (all branches)        │  against Pact Broker pacts
└──────────┬─────────────┘
           │
           ▼
┌────────────────────────┐
│  can-i-deploy          │  Ask Pact Broker: safe to deploy?
│  (main/develop)        │
└──────────┬─────────────┘
           │
           ▼
┌────────────────────────┐
│  deploy-provider       │  Build release artifact + deploy
│  (main only)           │
└──────────┬─────────────┘
           │
           ▼
┌────────────────────────┐
│  record-deployment     │  Tell Pact Broker: deployed to prod
│  (main only)           │
└────────────────────────┘

┌────────────────────────┐
│  notify-failure        │  Generate failure summary (on error)
└────────────────────────┘
```

### Required GitHub Secrets

| Secret | Description | Example |
|--------|-------------|---------|
| `PACT_BROKER_BASE_URL` | Pact Broker URL | `https://your-org.pactflow.io` |
| `PACT_BROKER_TOKEN` | API token (PactFlow) | `abc123...` |
| `PACT_BROKER_USERNAME` | Basic auth username (self-hosted) | `pact_user` |
| `PACT_BROKER_PASSWORD` | Basic auth password (self-hosted) | `pact_password` |

---

## How Contract Testing Works

### The Pact Workflow

```
1. CONSUMER (external repo) defines expectations → Pact JSON file
2. Consumer publishes pact to the Pact Broker
3. Pact Broker triggers this provider's CI via webhook
4. PROVIDER (this repo) verifies pact against the running API
5. Verification results published back to Pact Broker
6. can-i-deploy check gates deployment
```

### What Happens When the Provider Changes the API?

**Scenario:** Provider adds a required `NewPartNumber` field, but a consumer isn't sending it.

1. Provider developer makes the change
2. Provider verification runs → **FAILS** for that consumer's pact
3. `can-i-deploy` → **BLOCKED**
4. Provider team coordinates with consumer team
5. Consumer updates their pact to include the field
6. Provider re-verifies → **PASSES**
7. `can-i-deploy` → **SAFE** → Deploy

---

## Troubleshooting

| Issue | Cause | Solution |
|-------|-------|---------|
| Pact file not found | Consumer tests haven't run | Run consumer tests first, or verify pact exists in broker |
| Provider verification fails | API response doesn't match pact | Compare actual vs expected in test output |
| "No pacts found" from broker | No pacts published yet | Publish consumer pacts to broker first |
| Pact Broker connection refused | Docker not running | Start Docker Desktop, run setup script |
| `can-i-deploy` fails | Verification not yet recorded | Run provider verification tests first |

### Debugging Provider Verification

```bash
dotnet test src/Provider/NIOP.Provider.ContractTests/NIOP.Provider.ContractTests.csproj --verbosity detailed
```

---

## Best Practices

1. **Run provider verification on every PR** before merging API changes
2. **Use `can-i-deploy`** — never deploy without checking the Pact Broker
3. **Set up webhooks** — automatically verify when consumers publish new pacts
4. **Use provider states** in `ProviderWebApplicationFactory.cs` for consumer test preconditions
5. **Communicate breaking changes** to consumer teams proactively
6. **Keep contracts minimal** — consumers should only assert on fields they use
7. **Tag releases** — use `record-deployment` to track what's deployed

---

## Glossary

| Term | Definition |
|------|-----------|
| **Pact** | A contract (JSON file) between a consumer and provider defining expected interactions |
| **Consumer** | A system that calls the API (e.g., PCAW calling NIOP) |
| **Provider** | A system that exposes the API (NIOP Beat Inventory API) |
| **Pact Broker** | Central server storing pacts and verification results |
| **Provider State** | Precondition description for a specific interaction |
| **Verification** | Replaying consumer interactions against the real provider |
| **can-i-deploy** | Pact Broker CLI command checking if a version is safe to deploy |
| **Pending Pacts** | New pacts not yet verified (won't fail the build) |
| **Consumer Version Selector** | Rules for which consumer pact versions to verify |

---

*For provider team operations, see [docs/PROVIDER_GUIDE.md](docs/PROVIDER_GUIDE.md).  
For Pact Broker setup and management, see [docs/PACT_BROKER_GUIDE.md](docs/PACT_BROKER_GUIDE.md).*
