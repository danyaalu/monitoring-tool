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
    private readonly GotifyConfiguration _config;
    private readonly ILogger<GotifyNotificationService> _logger;

    public GotifyNotificationService(
        HttpClient httpClient, 
        GotifyConfiguration config,
        ILogger<GotifyNotificationService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task SendServerDownNotificationAsync(ServerStatus serverStatus, CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            _logger.LogDebug("Gotify notifications disabled, skipping server down notification for {ServerName}", serverStatus.ServerName);
            return;
        }

        var title = $"🔴 Server Down: {serverStatus.ServerName}";
        var message = $"Server '{serverStatus.ServerName}' is DOWN\n\n" +
                     $"⏰ Detected at: {serverStatus.LastChecked:yyyy-MM-dd HH:mm:ss} UTC\n" +
                     $"❌ Error: {serverStatus.ErrorMessage ?? "Connection failed"}";

        await SendNotificationAsync(title, message, 8, cancellationToken); // High priority for down alerts
    }

    public async Task SendServerUpNotificationAsync(ServerStatus serverStatus, CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            _logger.LogDebug("Gotify notifications disabled, skipping server up notification for {ServerName}", serverStatus.ServerName);
            return;
        }

        var title = $"🟢 Server Up: {serverStatus.ServerName}";
        var message = $"Server {serverStatus.ServerName} ({serverStatus.Host}:{serverStatus.Port}) is back UP\n\n" +
                     $"⏰ Restored at: {serverStatus.LastChecked:yyyy-MM-dd HH:mm:ss} UTC\n" +
                     $"⚡ Response time: {serverStatus.ResponseTime.TotalMilliseconds:F2}ms";

        await SendNotificationAsync(title, message, 3, cancellationToken); // Normal priority for up alerts
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

    private async Task SendNotificationAsync(string title, string message, int priority, CancellationToken cancellationToken = default)
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

            var url = $"{_config.BaseUrl.TrimEnd('/')}/message?token={_config.ApplicationToken}";
            
            _logger.LogDebug("Sending Gotify notification: {Title}", title);

            var response = await _httpClient.PostAsync(url, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully sent Gotify notification: {Title}", title);
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to send Gotify notification. Status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, responseContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Gotify notification: {Title}", title);
        }
    }
}