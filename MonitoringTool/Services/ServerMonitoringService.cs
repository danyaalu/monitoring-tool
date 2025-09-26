using MonitoringTool.Models;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MonitoringTool.Services;

public interface IServerMonitoringService
{
    Task<ServerStatus> CheckServerAsync(ServerConfiguration server, CancellationToken cancellationToken = default);
    Task<List<ServerStatus>> CheckAllServersAsync(List<ServerConfiguration> servers, CancellationToken cancellationToken = default);
}

public class ServerMonitoringService : IServerMonitoringService
{
    private readonly ILogger<ServerMonitoringService> _logger;
    private readonly int _timeoutMs;

    public ServerMonitoringService(ILogger<ServerMonitoringService> logger, int timeoutSeconds = 30)
    {
        _logger = logger;
        _timeoutMs = timeoutSeconds * 1000;
    }

    public async Task<ServerStatus> CheckServerAsync(ServerConfiguration server, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var status = new ServerStatus
        {
            ServerName = server.Name,
            Host = server.Host,
            Port = server.Port,
            LastChecked = DateTime.UtcNow
        };

        try
        {
            _logger.LogDebug("Checking server {ServerName} at {Host}:{Port}", server.Name, server.Host, server.Port);

            using var tcpClient = new TcpClient();
            var connectTask = tcpClient.ConnectAsync(server.Host, server.Port);
            var timeoutTask = Task.Delay(_timeoutMs, cancellationToken);

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                status.IsUp = false;
                status.ErrorMessage = $"Connection timeout after {_timeoutMs}ms";
            }
            else if (connectTask.IsFaulted)
            {
                status.IsUp = false;
                status.ErrorMessage = connectTask.Exception?.GetBaseException().Message ?? "Unknown connection error";
            }
            else
            {
                status.IsUp = tcpClient.Connected;
                if (!status.IsUp)
                {
                    status.ErrorMessage = "Unable to establish connection";
                }
            }

            stopwatch.Stop();
            status.ResponseTime = stopwatch.Elapsed;

            _logger.LogDebug("Server {ServerName} check completed: {Status} in {ResponseTime}ms", 
                server.Name, status.IsUp ? "UP" : "DOWN", status.ResponseTime.TotalMilliseconds);

            return status;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            status.IsUp = false;
            status.ErrorMessage = ex.Message;
            status.ResponseTime = stopwatch.Elapsed;

            _logger.LogError(ex, "Error checking server {ServerName} at {Host}:{Port}", 
                server.Name, server.Host, server.Port);

            return status;
        }
    }

    public async Task<List<ServerStatus>> CheckAllServersAsync(List<ServerConfiguration> servers, CancellationToken cancellationToken = default)
    {
        var enabledServers = servers.Where(s => s.Enabled).ToList();
        
        if (!enabledServers.Any())
        {
            _logger.LogWarning("No enabled servers found to monitor");
            return new List<ServerStatus>();
        }

        _logger.LogInformation("Checking {ServerCount} servers", enabledServers.Count);

        var tasks = enabledServers.Select(server => CheckServerAsync(server, cancellationToken));
        var results = await Task.WhenAll(tasks);

        var upCount = results.Count(r => r.IsUp);
        var downCount = results.Length - upCount;

        _logger.LogInformation("Server check completed: {UpCount} up, {DownCount} down", upCount, downCount);

        return results.ToList();
    }
}