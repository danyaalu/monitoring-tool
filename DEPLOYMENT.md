# 🚀 Monitoring Tool Deployment Guide

This guide shows you how to deploy the containerized monitoring tool on any server.

## 📋 Prerequisites on Target Server

1. **Docker and Docker Compose**
   ```bash
   # Ubuntu/Debian
   curl -fsSL https://get.docker.com -o get-docker.sh
   sudo sh get-docker.sh
   sudo usermod -aG docker $USER
   
   # Install Docker Compose (if not included)
   sudo curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
   sudo chmod +x /usr/local/bin/docker-compose
   
   # Log out and back in for group changes to take effect
   ```

2. **Git** (if not already installed)
   ```bash
   sudo apt update && sudo apt install git -y
   ```

## 🔄 Deployment Methods

### Method 1: Clone and Deploy (Recommended for Development)

```bash
# 1. Clone the repository
git clone https://github.com/danyaalu/monitoring-tool.git
cd monitoring-tool

# 2. Configure your settings
cp config/appsettings.Production.json config/appsettings.Production.json.backup
nano config/appsettings.Production.json  # Edit with your specific settings

# 3. Build and run
docker compose up --build -d

# 4. Check logs
docker compose logs -f monitoring-tool
```

### Method 2: Minimal Deployment (Production Ready)

This method only requires the essential files, not the entire source code:

```bash
# 1. Create deployment directory
mkdir monitoring-tool-deploy && cd monitoring-tool-deploy

# 2. Download essential files
wget https://raw.githubusercontent.com/danyaalu/monitoring-tool/main/Dockerfile
wget https://raw.githubusercontent.com/danyaalu/monitoring-tool/main/docker-compose.yml
wget https://raw.githubusercontent.com/danyaalu/monitoring-tool/main/.dockerignore

# 3. Create config directory and files
mkdir config
wget https://raw.githubusercontent.com/danyaalu/monitoring-tool/main/config/appsettings.json -O config/appsettings.json
wget https://raw.githubusercontent.com/danyaalu/monitoring-tool/main/config/appsettings.Production.json -O config/appsettings.Production.json

# 4. Configure your settings
nano config/appsettings.Production.json

# 5. Clone source code for building (temporary)
git clone https://github.com/danyaalu/monitoring-tool.git temp-source
cp -r temp-source/MonitoringTool .
cp temp-source/monitoring-tool.sln .
rm -rf temp-source

# 6. Build and run
docker compose up --build -d
```

### Method 3: Pre-built Image (Fastest)

If you publish your image to a registry:

```bash
# 1. Create deployment directory
mkdir monitoring-tool-deploy && cd monitoring-tool-deploy

# 2. Create docker-compose.yml for pre-built image
cat > docker-compose.yml << 'EOF'
version: '3.8'

services:
  monitoring-tool:
    image: your-registry/monitoring-tool:latest  # Replace with your image
    container_name: cardano-monitoring-tool
    restart: unless-stopped
    
    volumes:
      - ./config/appsettings.json:/app/appsettings.json:ro
      - ./config/appsettings.Production.json:/app/appsettings.Production.json:ro
      - ./logs:/app/logs
    
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - DOTNET_ENVIRONMENT=Production
    
    networks:
      - monitoring-network

networks:
  monitoring-network:
    driver: bridge
EOF

# 3. Create and configure settings
mkdir config
# Copy your configuration files here
nano config/appsettings.Production.json

# 4. Run
docker compose up -d
```

## ⚙️ Configuration Steps

### 1. Server Configuration
Edit `config/appsettings.Production.json`:

```json
{
  "Monitoring": {
    "CheckIntervalSeconds": 30,
    "Servers": [
      {
        "Name": "Your Server Name",
        "Host": "your-server-ip",
        "Port": 6000,
        "Enabled": true
      }
    ]
  }
}
```

### 2. Gotify Notifications (Optional)
Add your Gotify configuration:

```json
{
  "Monitoring": {
    "GotifyServers": [
      {
        "Name": "Production Monitor",
        "BaseUrl": "https://your-gotify-server.com",
        "ApplicationToken": "your-actual-token",
        "Priority": 5,
        "Enabled": true,
        "MonitorAllServers": true
      }
    ]
  }
}
```

## 🔧 Management Commands

```bash
# Start the monitoring tool
docker compose up -d

# View real-time logs
docker compose logs -f monitoring-tool

# Stop the monitoring tool
docker compose down

# Restart after configuration changes
docker compose restart monitoring-tool

# Update to latest version
docker compose down
docker compose pull  # If using pre-built image
# OR
docker compose up --build -d  # If building from source

# View container status
docker compose ps
```

## 📁 Directory Structure on Target Server

```
monitoring-tool/
├── docker-compose.yml          # Container orchestration
├── Dockerfile                  # Image build instructions
├── .dockerignore              # Build optimization
├── config/                    # Your configurations
│   ├── appsettings.json       # Base settings
│   └── appsettings.Production.json  # Production settings
├── logs/                      # Application logs (auto-created)
├── MonitoringTool/           # Source code (if building from source)
└── monitoring-tool.sln       # Solution file (if building from source)
```

## 🔒 Security Best Practices

1. **Protect sensitive files:**
   ```bash
   chmod 600 config/appsettings.Production.json
   ```

2. **Use environment variables for secrets:**
   ```yaml
   environment:
     - Monitoring__GotifyServers__0__ApplicationToken=${GOTIFY_TOKEN}
   ```

3. **Create a `.env` file for secrets:**
   ```bash
   echo "GOTIFY_TOKEN=your-actual-token" > .env
   chmod 600 .env
   ```

## 🚨 Troubleshooting

### Container won't start
```bash
# Check logs for errors
docker compose logs monitoring-tool

# Check configuration syntax
cat config/appsettings.Production.json | jq .  # Requires jq
```

### Configuration not loading
```bash
# Verify file mounts
docker compose exec monitoring-tool ls -la /app/

# Check environment variables
docker compose exec monitoring-tool env | grep ASPNET
```

### Network connectivity issues
```bash
# Test network connectivity from container
docker compose exec monitoring-tool ping 8.8.8.8

# Check if ports are accessible
docker compose exec monitoring-tool telnet your-server-ip your-port
```

## 🔄 Automated Deployment Script

Create `deploy.sh` for easy deployment:

```bash
#!/bin/bash
set -e

echo "🚀 Deploying Monitoring Tool..."

# Update configuration if provided
if [ "$1" = "config" ]; then
    echo "📝 Updating configuration..."
    nano config/appsettings.Production.json
fi

# Stop existing container
echo "🛑 Stopping existing container..."
docker compose down || true

# Pull latest changes (if git repo)
if [ -d ".git" ]; then
    echo "📥 Pulling latest changes..."
    git pull
fi

# Build and start
echo "🔨 Building and starting container..."
docker compose up --build -d

# Show logs
echo "📋 Showing logs (Ctrl+C to exit)..."
docker compose logs -f monitoring-tool
```

Make it executable:
```bash
chmod +x deploy.sh
./deploy.sh
```

## 📊 Monitoring the Monitor

Set up monitoring for the monitoring tool itself:

```bash
# Health check script
cat > health-check.sh << 'EOF'
#!/bin/bash
if docker compose ps monitoring-tool | grep -q "Up"; then
    echo "✅ Monitoring tool is running"
    exit 0
else
    echo "❌ Monitoring tool is down, restarting..."
    docker compose up -d
    exit 1
fi
EOF

chmod +x health-check.sh

# Add to crontab for automatic restart
echo "*/5 * * * * /path/to/monitoring-tool/health-check.sh" | crontab -
```

This comprehensive guide covers all scenarios from development to production deployment!