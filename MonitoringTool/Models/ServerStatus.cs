namespace MonitoringTool.Models;

public class ServerStatus
{
    public string ServerName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool IsUp { get; set; }
    public DateTime LastChecked { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan ResponseTime { get; set; }
}

public class ServerStatusChange
{
    public ServerStatus CurrentStatus { get; set; } = new();
    public ServerStatus? PreviousStatus { get; set; }
    public bool IsStatusChange => PreviousStatus?.IsUp != CurrentStatus.IsUp;
    public bool WentDown => PreviousStatus?.IsUp == true && CurrentStatus.IsUp == false;
    public bool CameUp => PreviousStatus?.IsUp == false && CurrentStatus.IsUp == true;
}