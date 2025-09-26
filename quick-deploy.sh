#!/bin/bash
set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}🚀 Monitoring Tool Deployment Script${NC}"
echo "=================================="

# Function to check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Check prerequisites
echo -e "${YELLOW}📋 Checking prerequisites...${NC}"

if ! command_exists docker; then
    echo -e "${RED}❌ Docker is not installed${NC}"
    echo "Please install Docker first: https://docs.docker.com/get-docker/"
    exit 1
fi

if ! command_exists git; then
    echo -e "${RED}❌ Git is not installed${NC}"
    echo "Please install Git first"
    exit 1
fi

echo -e "${GREEN}✅ Prerequisites satisfied${NC}"

# Get deployment method
echo ""
echo "Choose deployment method:"
echo "1) Full clone (includes source code, good for development)"
echo "2) Minimal deployment (production ready, smaller footprint)"
echo "3) Exit"
read -p "Enter your choice (1-3): " choice

case $choice in
    1)
        echo -e "${BLUE}📥 Cloning repository...${NC}"
        git clone https://github.com/danyaalu/monitoring-tool.git
        cd monitoring-tool
        ;;
    2)
        echo -e "${BLUE}📁 Setting up minimal deployment...${NC}"
        mkdir -p monitoring-tool-deploy
        cd monitoring-tool-deploy
        
        echo "Downloading essential files..."
        curl -sL https://raw.githubusercontent.com/danyaalu/monitoring-tool/main/Dockerfile -o Dockerfile
        curl -sL https://raw.githubusercontent.com/danyaalu/monitoring-tool/main/docker-compose.yml -o docker-compose.yml
        curl -sL https://raw.githubusercontent.com/danyaalu/monitoring-tool/main/.dockerignore -o .dockerignore
        
        mkdir -p config
        curl -sL https://raw.githubusercontent.com/danyaalu/monitoring-tool/main/config/appsettings.json -o config/appsettings.json
        curl -sL https://raw.githubusercontent.com/danyaalu/monitoring-tool/main/config/appsettings.Production.json -o config/appsettings.Production.json
        
        echo "Downloading source code for building..."
        git clone https://github.com/danyaalu/monitoring-tool.git temp-source
        cp -r temp-source/MonitoringTool .
        cp temp-source/monitoring-tool.sln .
        rm -rf temp-source
        ;;
    3)
        echo "Goodbye!"
        exit 0
        ;;
    *)
        echo -e "${RED}❌ Invalid choice${NC}"
        exit 1
        ;;
esac

# Configure settings
echo ""
echo -e "${YELLOW}⚙️ Configuration setup${NC}"
read -p "Do you want to edit the configuration now? (y/n): " edit_config

if [ "$edit_config" = "y" ] || [ "$edit_config" = "Y" ]; then
    echo "Opening configuration file..."
    if command_exists nano; then
        nano config/appsettings.Production.json
    elif command_exists vim; then
        vim config/appsettings.Production.json
    else
        echo "Please edit config/appsettings.Production.json manually"
        read -p "Press enter when done..."
    fi
fi

# Build and deploy
echo ""
echo -e "${BLUE}🔨 Building and starting the monitoring tool...${NC}"
docker compose up --build -d

echo ""
echo -e "${GREEN}✅ Deployment completed!${NC}"
echo ""
echo "Useful commands:"
echo "• View logs:           docker compose logs -f monitoring-tool"
echo "• Stop service:        docker compose down"
echo "• Restart service:     docker compose restart monitoring-tool"
echo "• Update config:       edit config/appsettings.Production.json, then docker compose restart monitoring-tool"
echo ""

# Show initial logs
echo -e "${BLUE}📋 Initial logs (Ctrl+C to exit):${NC}"
timeout 10 docker compose logs -f monitoring-tool || echo -e "\n${YELLOW}💡 Use 'docker compose logs -f monitoring-tool' to view logs anytime${NC}"