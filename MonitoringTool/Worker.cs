using Microsoft.Extensions.Options;
using MonitoringTool.Models;
using MonitoringTool.Services;

namespace MonitoringTool;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServerMonitoringService _monitoringService;
    private readonly IGotifyNotificationService _notificationService;
    private readonly MonitoringConfiguration _config;
    private readonly Dictionary<string, ServerStatus> _previousStatuses = new();

    public Worker(
        ILogger<Worker> logger,
        IServerMonitoringService monitoringService,
        IGotifyNotificationService notificationService,
        IOptions<MonitoringConfiguration> config)
    {
        _logger = logger;
        _monitoringService = monitoringService;
        _notificationService = notificationService;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Monitoring Tool started");
        _logger.LogInformation("Check interval: {Interval} seconds", _config.CheckIntervalSeconds);
        _logger.LogInformation("Timeout: {Timeout} seconds", _config.TimeoutSeconds);
        _logger.LogInformation("Servers to monitor: {ServerCount}", _config.Servers.Count(s => s.Enabled));

        // Initial startup delay
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformMonitoringCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Monitoring cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during monitoring cycle");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_config.CheckIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Monitoring Tool stopped");
    }

    private async Task PerformMonitoringCycleAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting monitoring cycle");

        var currentStatuses = await _monitoringService.CheckAllServersAsync(_config.Servers, cancellationToken);

        foreach (var currentStatus in currentStatuses)
        {
            var serverKey = $"{currentStatus.Host}:{currentStatus.Port}";
            
            // Get previous status if it exists
            _previousStatuses.TryGetValue(serverKey, out var previousStatus);

            // Create status change object
            var statusChange = new ServerStatusChange
            {
                CurrentStatus = currentStatus,
                PreviousStatus = previousStatus
            };

            // Log status
            if (currentStatus.IsUp)
            {
                _logger.LogInformation("✅ {ServerName} ({Host}:{Port}) is UP - Response: {ResponseTime}ms", 
                    currentStatus.ServerName, currentStatus.Host, currentStatus.Port, 
                    currentStatus.ResponseTime.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning("❌ {ServerName} ({Host}:{Port}) is DOWN - Error: {Error}", 
                    currentStatus.ServerName, currentStatus.Host, currentStatus.Port, 
                    currentStatus.ErrorMessage);
            }

            // Send notifications for status changes
            if (statusChange.IsStatusChange)
            {
                if (statusChange.WentDown)
                {
                    _logger.LogWarning("🔴 Server {ServerName} went DOWN", currentStatus.ServerName);
                }
                else if (statusChange.CameUp)
                {
                    _logger.LogInformation("🟢 Server {ServerName} came UP", currentStatus.ServerName);
                }

                await _notificationService.SendStatusChangeNotificationAsync(statusChange, cancellationToken);
            }

            // Update previous status
            _previousStatuses[serverKey] = currentStatus;
        }

        _logger.LogDebug("Monitoring cycle completed");
    }
}
