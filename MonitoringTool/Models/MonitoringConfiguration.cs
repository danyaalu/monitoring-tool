namespace MonitoringTool.Models;

public class MonitoringConfiguration
{
    public const string SectionName = "Monitoring";
    
    public List<ServerConfiguration> Servers { get; set; } = new();
    public List<GotifyConfiguration> GotifyServers { get; set; } = new();
    
    // Keep backwards compatibility
    public GotifyConfiguration Gotify { get; set; } = new();
    
    public int CheckIntervalSeconds { get; set; } = 30;
    public int TimeoutSeconds { get; set; } = 30;
}

public class ServerConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool Enabled { get; set; } = true;
}

public class GotifyConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApplicationToken { get; set; } = string.Empty;
    public int Priority { get; set; } = 5;
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// List of server names to monitor. If empty, monitors all servers.
    /// Use exact server names from the Servers configuration.
    /// </summary>
    public List<string> MonitoredServers { get; set; } = new();
    
    /// <summary>
    /// If true, monitors all servers (ignores MonitoredServers list).
    /// If false, only monitors servers listed in MonitoredServers.
    /// </summary>
    public bool MonitorAllServers { get; set; } = true;
}