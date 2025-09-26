# Monitoring Tool Docker Setup

This directory contains configuration files for running the Monitoring Tool in a Docker container.

## Quick Start

1. **Update Configuration**
   - Edit `config/appsettings.json` for basic settings
   - Edit `config/appsettings.Production.json` for production-specific settings including Gotify tokens

2. **Build and Run**
   ```bash
   docker-compose up --build -d
   ```

3. **View Logs**
   ```bash
   docker-compose logs -f monitoring-tool
   ```

## Configuration Methods

### Method 1: Configuration Files (Recommended)

The docker-compose.yml mounts configuration files from the `config/` directory:

- `config/appsettings.json` - Base configuration
- `config/appsettings.Production.json` - Production overrides (including sensitive data like tokens)

**Advantages:**
- Easy to edit configuration files
- Version control friendly (exclude sensitive files)
- Familiar configuration format

### Method 2: Environment Variables

You can override any configuration using environment variables. Add them to the `environment` section in docker-compose.yml:

```yaml
environment:
  - ASPNETCORE_ENVIRONMENT=Production
  - Monitoring__CheckIntervalSeconds=60
  - Monitoring__TimeoutSeconds=30
  - Monitoring__Servers__0__Name=Custom Server
  - Monitoring__Servers__0__Host=192.168.1.100
  - Monitoring__Servers__0__Port=6000
  - Monitoring__GotifyServers__0__ApplicationToken=your_token_here
```

**Configuration Path Format:**
- Use double underscores (`__`) to represent nested properties
- Use numbers for array indices
- Example: `Monitoring__Servers__0__Host` sets the host of the first server

### Method 3: Docker Secrets (For Production)

For production environments, you can use Docker secrets for sensitive data:

```yaml
secrets:
  gotify_token:
    external: true
    
services:
  monitoring-tool:
    secrets:
      - gotify_token
    environment:
      - Monitoring__GotifyServers__0__ApplicationToken_File=/run/secrets/gotify_token
```

## File Structure

```
monitoring-tool/
├── Dockerfile
├── docker-compose.yml
├── config/
│   ├── appsettings.json              # Base configuration
│   └── appsettings.Production.json   # Production overrides
├── logs/                            # Container logs (if enabled)
└── README.md                        # This file
```

## Configuration Examples

### Basic Server Monitoring
```json
{
  "Monitoring": {
    "CheckIntervalSeconds": 60,
    "TimeoutSeconds": 30,
    "Servers": [
      {
        "Name": "Web Server",
        "Host": "192.168.1.100",
        "Port": 80,
        "Enabled": true
      }
    ]
  }
}
```

### Gotify Notifications
```json
{
  "Monitoring": {
    "GotifyServers": [
      {
        "Name": "Main Notifications",
        "BaseUrl": "https://gotify.yourdomain.com",
        "ApplicationToken": "your_application_token",
        "Priority": 5,
        "Enabled": true,
        "MonitorAllServers": true
      }
    ]
  }
}
```

## Docker Commands

### Build the image
```bash
docker build -t monitoring-tool .
```

### Run with docker-compose
```bash
docker-compose up -d
```

### View logs
```bash
docker-compose logs -f
```

### Stop the container
```bash
docker-compose down
```

### Restart the container
```bash
docker-compose restart
```

### Update configuration and restart
```bash
# Edit config files
docker-compose restart monitoring-tool
```

## Security Considerations

1. **Sensitive Data**: Keep sensitive data like API tokens in `appsettings.Production.json` and exclude from version control
2. **Network Security**: Use Docker networks to isolate the container
3. **User Permissions**: The container runs as a non-root user
4. **Read-only Config**: Configuration files are mounted as read-only

## Troubleshooting

### Container won't start
- Check logs: `docker-compose logs monitoring-tool`
- Verify configuration syntax: Use a JSON validator
- Check file permissions on config directory

### Configuration not loading
- Ensure config files are mounted correctly
- Check environment variable names (use double underscores)
- Verify JSON syntax in configuration files

### Network connectivity issues
- Ensure target servers are accessible from container network
- Check firewall settings
- Test connectivity: `docker exec -it cardano-monitoring-tool ping target-host`