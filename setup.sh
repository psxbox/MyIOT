#!/bin/bash
set -e

echo "╔════════════════════════════════════════════════════════════════╗"
echo "║         MyIOT Platform - Quick Start Script                   ║"
echo "╚════════════════════════════════════════════════════════════════╝"
echo ""

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Check prerequisites
echo -e "${YELLOW}[1/7] Checking prerequisites...${NC}"

if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}✗ .NET SDK not found. Install: https://dotnet.microsoft.com/download${NC}"
    exit 1
fi

if ! command -v docker &> /dev/null; then
    echo -e "${RED}✗ Docker not found. Install: https://docs.docker.com/get-docker/${NC}"
    exit 1
fi

if ! command -v docker-compose &> /dev/null; then
    echo -e "${RED}✗ Docker Compose not found.${NC}"
    exit 1
fi

echo -e "${GREEN}✓ All prerequisites found${NC}"
echo ""

# Start infrastructure
echo -e "${YELLOW}[2/7] Starting PostgreSQL/TimescaleDB and Redis...${NC}"
docker-compose up -d

# Wait for PostgreSQL to be ready
echo -e "${YELLOW}[3/7] Waiting for database to be ready...${NC}"
sleep 5

until docker exec myiot-timescaledb pg_isready -U myiot -d myiot_db &> /dev/null; do
    echo "  Waiting for PostgreSQL..."
    sleep 2
done

echo -e "${GREEN}✓ Database is ready${NC}"
echo ""

# Check if migrations exist
if [ ! -d "src/MyIOT.Api/Migrations" ]; then
    echo -e "${YELLOW}[4/7] Creating initial migration...${NC}"
    
    # Install EF Core tools if not present
    if ! dotnet ef --version &> /dev/null; then
        echo "  Installing dotnet-ef tool..."
        dotnet tool install --global dotnet-ef
        export PATH="$PATH:$HOME/.dotnet/tools"
    fi
    
    dotnet ef migrations add InitialCreate --project src/MyIOT.Api
    echo -e "${GREEN}✓ Migration created${NC}"
else
    echo -e "${GREEN}[4/7] Migrations already exist${NC}"
fi
echo ""

# Build solution
echo -e "${YELLOW}[5/7] Building solution...${NC}"
dotnet build --configuration Release
echo -e "${GREEN}✓ Build successful${NC}"
echo ""

# Run tests
echo -e "${YELLOW}[6/7] Running unit tests...${NC}"
dotnet test --no-build --configuration Release --verbosity minimal
echo -e "${GREEN}✓ All tests passed${NC}"
echo ""

# Create sample device
echo -e "${YELLOW}[7/7] Starting API server in background...${NC}"
dotnet run --project src/MyIOT.Api --no-build --configuration Release &
API_PID=$!

# Wait for API to start
echo "  Waiting for API to start..."
sleep 10

# Check if API is running
if ! curl -s http://localhost:5000/swagger/index.html > /dev/null 2>&1; then
    echo -e "${RED}✗ API failed to start${NC}"
    kill $API_PID 2>/dev/null || true
    exit 1
fi

echo -e "${GREEN}✓ API started successfully${NC}"
echo ""

# Create test device
echo -e "${YELLOW}Creating test device...${NC}"
DEVICE_RESPONSE=$(curl -s -X POST http://localhost:5000/api/devices \
    -H "Content-Type: application/json" \
    -d '{"name": "DemoDevice"}')

ACCESS_TOKEN=$(echo $DEVICE_RESPONSE | grep -o '"accessToken":"[^"]*"' | cut -d'"' -f4)
DEVICE_ID=$(echo $DEVICE_RESPONSE | grep -o '"id":"[^"]*"' | cut -d'"' -f4)

if [ -z "$ACCESS_TOKEN" ]; then
    echo -e "${RED}✗ Failed to create device${NC}"
    kill $API_PID 2>/dev/null || true
    exit 1
fi

echo -e "${GREEN}✓ Device created${NC}"
echo ""

# Get JWT token
echo -e "${YELLOW}Getting JWT token...${NC}"
TOKEN_RESPONSE=$(curl -s -X POST http://localhost:5000/api/auth/device/login \
    -H "Content-Type: application/json" \
    -d "{\"accessToken\": \"$ACCESS_TOKEN\"}")

JWT_TOKEN=$(echo $TOKEN_RESPONSE | grep -o '"token":"[^"]*"' | cut -d'"' -f4)

if [ -z "$JWT_TOKEN" ]; then
    echo -e "${RED}✗ Failed to get JWT token${NC}"
    kill $API_PID 2>/dev/null || true
    exit 1
fi

echo -e "${GREEN}✓ JWT token obtained${NC}"
echo ""

# Send test telemetry
echo -e "${YELLOW}Sending test telemetry...${NC}"
curl -s -X POST http://localhost:5000/api/telemetry \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer $JWT_TOKEN" \
    -d '{"values": {"temperature": 23.5, "humidity": 55.0, "pressure": 1013.25}}' > /dev/null

echo -e "${GREEN}✓ Telemetry sent${NC}"
echo ""

# Summary
echo "╔════════════════════════════════════════════════════════════════╗"
echo "║                    🎉 Setup Complete! 🎉                       ║"
echo "╚════════════════════════════════════════════════════════════════╝"
echo ""
echo -e "${GREEN}Services:${NC}"
echo "  • HTTP API:    http://localhost:5000"
echo "  • Swagger UI:  http://localhost:5000/swagger"
echo "  • MQTT Broker: localhost:1883"
echo "  • PostgreSQL:  localhost:5432 (user: myiot, pass: myiot_secret)"
echo "  • Redis:       localhost:6379"
echo ""
echo -e "${GREEN}Demo Device:${NC}"
echo "  • Device ID:    $DEVICE_ID"
echo "  • Access Token: $ACCESS_TOKEN"
echo "  • JWT Token:    ${JWT_TOKEN:0:50}..."
echo ""
echo -e "${YELLOW}Quick commands:${NC}"
echo "  # Get latest telemetry"
echo "  curl -H \"Authorization: Bearer $JWT_TOKEN\" \\"
echo "    http://localhost:5000/api/devices/$DEVICE_ID/telemetry/latest"
echo ""
echo "  # Send more telemetry"
echo "  curl -X POST http://localhost:5000/api/telemetry \\"
echo "    -H \"Authorization: Bearer $JWT_TOKEN\" \\"
echo "    -H \"Content-Type: application/json\" \\"
echo "    -d '{\"values\": {\"temperature\": 25.0}}'"
echo ""
echo -e "${YELLOW}API server PID: $API_PID${NC}"
echo "To stop the server: kill $API_PID"
echo "To stop infrastructure: docker-compose down"
echo ""
