using MonitoringTool.Models;
using System.Text;
using System.Text.Json;

namespace MonitoringTool.Services;

public interface IGotifyNotificationService
{
    Task SendServerDownNotificationAsync(ServerStatus serverStatus, CancellationToken cancellationToken = default);
    Task SendServerUpNotificationAsync(ServerStatus serverStatus, CancellationToken cancellationToken = default);
    Task SendStatusChangeNotificationAsync(ServerStatusChange statusChange, CancellationToken cancellationToken = default);
}

public class GotifyNotificationService : IGotifyNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly List<GotifyConfiguration> _configs;
    private readonly ILogger<GotifyNotificationService> _logger;

    public GotifyNotificationService(
        HttpClient httpClient, 
        MonitoringConfiguration monitoringConfig,
        ILogger<GotifyNotificationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // Support both single and multiple Gotify configurations
        _configs = new List<GotifyConfiguration>();
        
        // Add multiple Gotify servers if configured
        if (monitoringConfig.GotifyServers?.Any() == true)
        {
            _configs.AddRange(monitoringConfig.GotifyServers.Where(g => g.Enabled));
        }
        
        // Add legacy single Gotify config for backwards compatibility
        if (monitoringConfig.Gotify?.Enabled == true && !string.IsNullOrEmpty(monitoringConfig.Gotify.BaseUrl))
        {
            _configs.Add(monitoringConfig.Gotify);
        }
        
        _logger.LogInformation("Initialized Gotify notification service with {ServerCount} servers", _configs.Count);
        
        // Log which servers each Gotify instance will monitor
        foreach (var config in _configs)
        {
            var configName = !string.IsNullOrEmpty(config.Name) ? config.Name : config.BaseUrl;
            if (config.MonitorAllServers)
            {
                _logger.LogInformation("Gotify server '{GotifyName}' will monitor ALL servers", configName);
            }
            else if (config.MonitoredServers.Any())
            {
                _logger.LogInformation("Gotify server '{GotifyName}' will monitor servers: {MonitoredServers}", 
                    configName, string.Join(", ", config.MonitoredServers));
            }
            else
            {
                _logger.LogWarning("Gotify server '{GotifyName}' has MonitorAllServers=false but no specific servers listed - will not receive notifications", configName);
            }
        }
    }

    public async Task SendServerDownNotificationAsync(ServerStatus serverStatus, CancellationToken cancellationToken = default)
    {
        var applicableConfigs = GetApplicableGotifyConfigs(serverStatus.ServerName);
        
        if (!applicableConfigs.Any())
        {
            _logger.LogDebug("No Gotify servers configured for server {ServerName}, skipping server down notification", serverStatus.ServerName);
            return;
        }

        var title = $"🔴 Server Down: {serverStatus.ServerName}";
        var message = $"Server '{serverStatus.ServerName}' is DOWN\n\n" +
                     $"⏰ Detected at: {serverStatus.LastChecked:yyyy-MM-dd HH:mm:ss} UTC\n" +
                     $"❌ Error: {serverStatus.ErrorMessage ?? "Connection failed"}";

        await SendNotificationToSpecificServersAsync(applicableConfigs, title, message, 8, cancellationToken); // High priority for down alerts
    }

    public async Task SendServerUpNotificationAsync(ServerStatus serverStatus, CancellationToken cancellationToken = default)
    {
        var applicableConfigs = GetApplicableGotifyConfigs(serverStatus.ServerName);
        
        if (!applicableConfigs.Any())
        {
            _logger.LogDebug("No Gotify servers configured for server {ServerName}, skipping server up notification", serverStatus.ServerName);
            return;
        }

        var title = $"🟢 Server Up: {serverStatus.ServerName}";
        var message = $"Server {serverStatus.ServerName} ({serverStatus.Host}:{serverStatus.Port}) is back UP\n\n" +
                     $"⏰ Restored at: {serverStatus.LastChecked:yyyy-MM-dd HH:mm:ss} UTC\n" +
                     $"⚡ Response time: {serverStatus.ResponseTime.TotalMilliseconds:F2}ms";

        await SendNotificationToSpecificServersAsync(applicableConfigs, title, message, 3, cancellationToken); // Normal priority for up alerts
    }

    public async Task SendStatusChangeNotificationAsync(ServerStatusChange statusChange, CancellationToken cancellationToken = default)
    {
        if (!statusChange.IsStatusChange)
        {
            return;
        }

        if (statusChange.WentDown)
        {
            await SendServerDownNotificationAsync(statusChange.CurrentStatus, cancellationToken);
        }
        else if (statusChange.CameUp)
        {
            await SendServerUpNotificationAsync(statusChange.CurrentStatus, cancellationToken);
        }
    }

    private List<GotifyConfiguration> GetApplicableGotifyConfigs(string serverName)
    {
        return _configs.Where(config => ShouldNotifyForServer(config, serverName)).ToList();
    }
    
    private bool ShouldNotifyForServer(GotifyConfiguration config, string serverName)
    {
        // If MonitorAllServers is true, notify for all servers
        if (config.MonitorAllServers)
        {
            return true;
        }
        
        // If MonitoredServers is empty but MonitorAllServers is false, don't notify
        if (!config.MonitoredServers.Any())
        {
            return false;
        }
        
        // Check if this server is in the monitored servers list (case-insensitive)
        return config.MonitoredServers.Any(monitoredServer => 
            string.Equals(monitoredServer.Trim(), serverName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private async Task SendNotificationToSpecificServersAsync(List<GotifyConfiguration> configs, string title, string message, int priority, CancellationToken cancellationToken = default)
    {
        if (!configs.Any())
        {
            _logger.LogDebug("No applicable Gotify servers for notification: {Title}", title);
            return;
        }
        
        var tasks = configs.Select(config => SendNotificationToServerAsync(config, title, message, priority, cancellationToken));
        await Task.WhenAll(tasks);
    }

    private async Task SendNotificationToAllServersAsync(string title, string message, int priority, CancellationToken cancellationToken = default)
    {
        var tasks = _configs.Select(config => SendNotificationToServerAsync(config, title, message, priority, cancellationToken));
        await Task.WhenAll(tasks);
    }

    private async Task SendNotificationToServerAsync(GotifyConfiguration config, string title, string message, int priority, CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = new
            {
                title = title,
                message = message,
                priority = priority
            };

            var json = JsonSerializer.Serialize(notification);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{config.BaseUrl.TrimEnd('/')}/message?token={config.ApplicationToken}";
            
            _logger.LogDebug("Sending Gotify notification to {ServerName}: {Title}", 
                !string.IsNullOrEmpty(config.Name) ? config.Name : config.BaseUrl, title);

            var response = await _httpClient.PostAsync(url, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully sent Gotify notification to {ServerName}: {Title}", 
                    !string.IsNullOrEmpty(config.Name) ? config.Name : config.BaseUrl, title);
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to send Gotify notification to {ServerName}. Status: {StatusCode}, Response: {Response}", 
                    !string.IsNullOrEmpty(config.Name) ? config.Name : config.BaseUrl, response.StatusCode, responseContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Gotify notification to {ServerName}: {Title}", 
                !string.IsNullOrEmpty(config.Name) ? config.Name : config.BaseUrl, title);
        }
    }
}