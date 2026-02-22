# Pact Broker Operations Guide

> Setting up, managing, and maintaining the Pact Broker for NIOP contract testing

---

## Overview

The Pact Broker is the **central hub** for contract testing. It stores consumer pacts, tracks provider verification results, and provides the `can-i-deploy` safety gate.

---

## Local Setup

### Prerequisites

- Docker Desktop installed and running
- Ports 5432 (PostgreSQL) and 9292 (Pact Broker) available

### Starting the Broker

**Windows:**
```powershell
cd infrastructure
.\setup-pact-broker.ps1
```

**Linux/macOS:**
```bash
cd infrastructure
chmod +x setup-pact-broker.sh
./setup-pact-broker.sh
```

**Manual:**
```bash
docker-compose -f infrastructure/docker-compose.pact-broker.yml up -d
```

### Verifying It's Running

```bash
curl http://localhost:9292/diagnostic/status/heartbeat
# Expected: {"ok":true}
```

### Default Credentials

| Service | Username | Password |
|---------|----------|----------|
| Pact Broker | pact_user | pact_password |
| PostgreSQL | pact_broker | pact_broker_password |

> **Change these in production!**

---

## Pact Broker UI

Access the Pact Broker at: **http://localhost:9292**

### Key Pages

| Page | Path | Description |
|------|------|-------------|
| Dashboard | `/` | All pacts and their verification status |
| Matrix | `/matrix` | Compatibility matrix between all versions |
| Network | `/groups` | Visual dependency diagram |
| Settings | `/settings` | Broker configuration |
| API Docs | `/docs` | HAL browser for the API |

---

## Publishing Pacts

Consumer teams publish pacts from their own repositories. For local testing, you can publish pact files manually:

```bash
# Install Pact CLI (one time)
# Windows (via Chocolatey):
choco install pact-ruby-standalone

# Or download from: https://github.com/pact-foundation/pact-ruby-standalone/releases

# Publish a pact file
pact-broker publish pacts/PCAW-Consumer-NIOP-Beat-Inventory-Api.json \
  --consumer-app-version="1.0.0-local" \
  --branch="main" \
  --broker-base-url="http://localhost:9292" \
  --broker-username="pact_user" \
  --broker-password="pact_password"

# Publish all pacts in the directory
pact-broker publish pacts/ \
  --consumer-app-version="$(git rev-parse HEAD)" \
  --branch="$(git branch --show-current)" \
  --broker-base-url="http://localhost:9292" \
  --broker-username="pact_user" \
  --broker-password="pact_password" \
  --tag-with-git-branch
```

---

## Can-I-Deploy

The `can-i-deploy` command is the **safety gate** that prevents broken deployments.

### Check the Provider

```bash
pact-broker can-i-deploy \
  --pacticipant="NIOP-Beat-Inventory-Api" \
  --version="abc123" \
  --to-environment="production" \
  --broker-base-url="http://localhost:9292" \
  --broker-username="pact_user" \
  --broker-password="pact_password"
```

### Check a Consumer

```bash
pact-broker can-i-deploy \
  --pacticipant="PCAW-Consumer" \
  --version="abc123" \
  --to-environment="production" \
  --broker-base-url="http://localhost:9292" \
  --broker-username="pact_user" \
  --broker-password="pact_password"
```

### Output Examples

**Safe to deploy:**
```
Computer says yes \o/

CONSUMER       | C.VERSION | PROVIDER                | P.VERSION | SUCCESS?
---------------|-----------|-------------------------|-----------|--------
PCAW-Consumer  | abc123    | NIOP-Beat-Inventory-Api | def456    | true
```

**Not safe:**
```
Computer says no ¯\_(ツ)_/¯

CONSUMER       | C.VERSION | PROVIDER                | P.VERSION | SUCCESS?
---------------|-----------|-------------------------|-----------|--------
PCAW-Consumer  | abc123    | NIOP-Beat-Inventory-Api | def456    | false
```

---

## Recording Deployments

After a successful deployment, record it:

```bash
# Record a deployment to production
pact-broker record-deployment \
  --pacticipant="NIOP-Beat-Inventory-Api" \
  --version="abc123" \
  --environment="production" \
  --broker-base-url="http://localhost:9292" \
  --broker-username="pact_user" \
  --broker-password="pact_password"
```

---

## Creating Environments

```bash
pact-broker create-environment \
  --name="production" \
  --production \
  --broker-base-url="http://localhost:9292"

pact-broker create-environment \
  --name="staging" \
  --broker-base-url="http://localhost:9292"
```

---

## Webhooks

### Create Provider Verification Webhook

This webhook triggers the provider CI when any consumer publishes a new pact:

```bash
pact-broker create-webhook \
  "https://api.github.com/repos/YOUR_ORG/NIOP_PartNumberEndpoints/dispatches" \
  --request=POST \
  --header "Content-Type: application/json" \
  --header "Authorization: Bearer GITHUB_PAT" \
  --data '{"event_type":"pact-changed","client_payload":{"pact_url":"${pactbroker.pactUrl}"}}' \
  --provider="NIOP-Beat-Inventory-Api" \
  --contract-content-changed \
  --broker-base-url="http://localhost:9292"
```

### List Webhooks

```bash
pact-broker list-webhooks \
  --broker-base-url="http://localhost:9292"
```

---

## Production Deployment Options

### Option 1: PactFlow (Recommended for Enterprise)

[PactFlow](https://pactflow.io) is a managed Pact Broker SaaS:
- No infrastructure to manage
- Built-in authentication and SSO
- Advanced features (bi-directional testing, schema-based contracts)
- SLA-backed uptime

### Option 2: Self-Hosted (Docker)

Use the provided `docker-compose.pact-broker.yml` with production modifications:
- Use external PostgreSQL (AWS RDS, Azure SQL, etc.)
- Put behind a reverse proxy (nginx, ALB)
- Enable HTTPS
- Use strong passwords
- Configure backup strategy

### Option 3: Kubernetes

```bash
helm repo add pact-broker https://pact-foundation.github.io/pact-broker-chart
helm install pact-broker pact-broker/pact-broker
```

---

## Backup and Recovery

### Backup PostgreSQL Data

```bash
docker exec niop-pact-postgres pg_dump -U pact_broker pact_broker > pact_broker_backup.sql
docker exec -i niop-pact-postgres psql -U pact_broker pact_broker < pact_broker_backup.sql
```

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Broker won't start | PostgreSQL not ready | Wait 30s and retry, check Docker logs |
| "Connection refused" | Docker not running | Start Docker Desktop |
| Port 9292 in use | Another service on that port | Change port in docker-compose or stop conflicting service |
| Webhook not firing | Misconfigured URL | Check webhook config, verify GitHub PAT |
| Old pacts cluttering UI | Pacts not cleaned up | Use `pact-broker delete-branch` for old branches |

---

## API Reference

The Pact Broker has a RESTful API. Access the HAL browser at: http://localhost:9292

### Useful API Endpoints

```bash
# List all pacts
curl http://localhost:9292/pacts

# Get latest pact for a consumer
curl http://localhost:9292/pacts/provider/NIOP-Beat-Inventory-Api/consumer/PCAW-Consumer/latest

# Get verification results
curl http://localhost:9292/pacts/provider/NIOP-Beat-Inventory-Api/consumer/PCAW-Consumer/latest/verification-results

# Get the matrix
curl "http://localhost:9292/matrix?q[][pacticipant]=NIOP-Beat-Inventory-Api&latestby=cvpv"
```
