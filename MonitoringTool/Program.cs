using MonitoringTool;
using MonitoringTool.Models;
using MonitoringTool.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configure options
builder.Services.Configure<MonitoringConfiguration>(
    builder.Configuration.GetSection(MonitoringConfiguration.SectionName));

// Register HttpClient for Gotify notifications
builder.Services.AddHttpClient<IGotifyNotificationService, GotifyNotificationService>((serviceProvider, httpClient) =>
{
    // Configure any default headers or timeout if needed
    httpClient.Timeout = TimeSpan.FromSeconds(30);
});

// Register services
builder.Services.AddTransient<IServerMonitoringService>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<ServerMonitoringService>>();
    var config = builder.Configuration.GetSection(MonitoringConfiguration.SectionName).Get<MonitoringConfiguration>();
    return new ServerMonitoringService(logger, config?.TimeoutSeconds ?? 30);
});

builder.Services.AddTransient<IGotifyNotificationService>(serviceProvider =>
{
    var httpClient = serviceProvider.GetRequiredService<HttpClient>();
    var logger = serviceProvider.GetRequiredService<ILogger<GotifyNotificationService>>();
    var config = builder.Configuration.GetSection(MonitoringConfiguration.SectionName).Get<MonitoringConfiguration>();
    return new GotifyNotificationService(httpClient, config?.Gotify ?? new GotifyConfiguration(), logger);
});

// Register the hosted service
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Log startup information
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting Monitoring Tool...");

host.Run();
