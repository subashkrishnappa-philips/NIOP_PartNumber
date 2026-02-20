# ============================================================================
# PowerShell Setup Script for Pact Broker (Windows)
# ============================================================================
# Starts the Pact Broker infrastructure and verifies it's running.
#
# Prerequisites:
#   - Docker Desktop for Windows installed and running
#   - Ports 5432 and 9292 available
#
# Usage:
#   .\setup-pact-broker.ps1
# ============================================================================

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ComposeFile = Join-Path $ScriptDir "docker-compose.pact-broker.yml"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  NIOP Pact Broker Setup" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# Check prerequisites
Write-Host "[1/5] Checking prerequisites..." -ForegroundColor Yellow
try {
    docker version | Out-Null
}
catch {
    Write-Host "ERROR: Docker is not installed or not running. Please install Docker Desktop." -ForegroundColor Red
    exit 1
}

# Stop existing containers
Write-Host "[2/5] Stopping existing Pact Broker containers..." -ForegroundColor Yellow
docker-compose -f $ComposeFile down --remove-orphans 2>$null

# Start services
Write-Host "[3/5] Starting Pact Broker services..." -ForegroundColor Yellow
docker-compose -f $ComposeFile up -d

# Wait for services
Write-Host "[4/5] Waiting for services to be healthy..." -ForegroundColor Yellow
$maxRetries = 30
$retryCount = 0

while ($retryCount -lt $maxRetries) {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:9292/diagnostic/status/heartbeat" -UseBasicParsing -TimeoutSec 3
        if ($response.StatusCode -eq 200) {
            Write-Host "Pact Broker is healthy!" -ForegroundColor Green
            break
        }
    }
    catch {
        # Service not ready yet
    }
    $retryCount++
    Write-Host "  Waiting for Pact Broker to start (attempt $retryCount/$maxRetries)..."
    Start-Sleep -Seconds 2
}

if ($retryCount -eq $maxRetries) {
    Write-Host "ERROR: Pact Broker did not start in time. Check Docker logs:" -ForegroundColor Red
    Write-Host "  docker-compose -f $ComposeFile logs" -ForegroundColor Red
    exit 1
}

# Display info
Write-Host "[5/5] Setup complete!" -ForegroundColor Yellow
Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "  Pact Broker is Running" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
Write-Host "  URL:      http://localhost:9292" -ForegroundColor White
Write-Host "  Username: pact_user" -ForegroundColor White
Write-Host "  Password: pact_password" -ForegroundColor White
Write-Host ""
Write-Host "  To stop:  docker-compose -f $ComposeFile down" -ForegroundColor Gray
Write-Host "  Logs:     docker-compose -f $ComposeFile logs -f" -ForegroundColor Gray
Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
