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
    }

    public async Task SendServerDownNotificationAsync(ServerStatus serverStatus, CancellationToken cancellationToken = default)
    {
        if (!_configs.Any())
        {
            _logger.LogDebug("No Gotify servers configured, skipping server down notification for {ServerName}", serverStatus.ServerName);
            return;
        }

        var title = $"🔴 Server Down: {serverStatus.ServerName}";
        var message = $"Server '{serverStatus.ServerName}' is DOWN\n\n" +
                     $"⏰ Detected at: {serverStatus.LastChecked:yyyy-MM-dd HH:mm:ss} UTC\n" +
                     $"❌ Error: {serverStatus.ErrorMessage ?? "Connection failed"}";

        await SendNotificationToAllServersAsync(title, message, 8, cancellationToken); // High priority for down alerts
    }

    public async Task SendServerUpNotificationAsync(ServerStatus serverStatus, CancellationToken cancellationToken = default)
    {
        if (!_configs.Any())
        {
            _logger.LogDebug("No Gotify servers configured, skipping server up notification for {ServerName}", serverStatus.ServerName);
            return;
        }

        var title = $"🟢 Server Up: {serverStatus.ServerName}";
        var message = $"Server {serverStatus.ServerName} ({serverStatus.Host}:{serverStatus.Port}) is back UP\n\n" +
                     $"⏰ Restored at: {serverStatus.LastChecked:yyyy-MM-dd HH:mm:ss} UTC\n" +
                     $"⚡ Response time: {serverStatus.ResponseTime.TotalMilliseconds:F2}ms";

        await SendNotificationToAllServersAsync(title, message, 3, cancellationToken); // Normal priority for up alerts
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