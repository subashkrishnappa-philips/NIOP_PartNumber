#!/bin/bash
# ============================================================================
# Pact Broker Setup Script
# ============================================================================
# Starts the Pact Broker infrastructure and verifies it's running.
#
# Prerequisites:
#   - Docker and Docker Compose installed
#   - Ports 5432 and 9292 available
#
# Usage:
#   chmod +x setup-pact-broker.sh
#   ./setup-pact-broker.sh
# ============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_FILE="$SCRIPT_DIR/docker-compose.pact-broker.yml"

echo "=========================================="
echo "  NIOP Pact Broker Setup"
echo "=========================================="

# Check prerequisites
echo "[1/5] Checking prerequisites..."
if ! command -v docker &> /dev/null; then
    echo "ERROR: Docker is not installed. Please install Docker first."
    exit 1
fi

if ! command -v docker-compose &> /dev/null && ! docker compose version &> /dev/null; then
    echo "ERROR: Docker Compose is not installed. Please install Docker Compose first."
    exit 1
fi

# Stop any existing containers
echo "[2/5] Stopping existing Pact Broker containers..."
docker-compose -f "$COMPOSE_FILE" down --remove-orphans 2>/dev/null || true

# Start services
echo "[3/5] Starting Pact Broker services..."
docker-compose -f "$COMPOSE_FILE" up -d

# Wait for services to be healthy
echo "[4/5] Waiting for services to be healthy..."
MAX_RETRIES=30
RETRY_COUNT=0

while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
    if curl -s -o /dev/null -w "%{http_code}" http://localhost:9292/diagnostic/status/heartbeat | grep -q "200"; then
        echo "Pact Broker is healthy!"
        break
    fi
    RETRY_COUNT=$((RETRY_COUNT + 1))
    echo "  Waiting for Pact Broker to start (attempt $RETRY_COUNT/$MAX_RETRIES)..."
    sleep 2
done

if [ $RETRY_COUNT -eq $MAX_RETRIES ]; then
    echo "ERROR: Pact Broker did not start in time. Check Docker logs:"
    echo "  docker-compose -f $COMPOSE_FILE logs"
    exit 1
fi

# Display info
echo "[5/5] Setup complete!"
echo ""
echo "=========================================="
echo "  Pact Broker is Running"
echo "=========================================="
echo ""
echo "  URL:      http://localhost:9292"
echo "  Username: pact_user"
echo "  Password: pact_password"
echo ""
echo "  To stop:  docker-compose -f $COMPOSE_FILE down"
echo "  Logs:     docker-compose -f $COMPOSE_FILE logs -f"
echo ""
echo "=========================================="
