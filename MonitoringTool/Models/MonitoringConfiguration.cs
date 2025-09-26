namespace MonitoringTool.Models;

public class MonitoringConfiguration
{
    public const string SectionName = "Monitoring";
    
    public List<ServerConfiguration> Servers { get; set; } = new();
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
    public string BaseUrl { get; set; } = string.Empty;
    public string ApplicationToken { get; set; } = string.Empty;
    public int Priority { get; set; } = 5;
    public bool Enabled { get; set; } = true;
}