using yQuant.App.BrokerGateway;
using yQuant.Infra.Redis.Extensions;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Broker.KIS;
using yQuant.Infra.Notification;
using yQuant.Infra.Notification.Discord;
using StackExchange.Redis;



var settings = new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
};

var builder = Host.CreateApplicationBuilder(settings);

// Load configuration files
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                     .AddJsonFile("appsecrets.json", optional: false, reloadOnChange: true);


// Register KIS HttpClient
builder.Services.AddHttpClient("KIS");

// Redis
builder.Services.AddRedisMiddleware(builder.Configuration)
                .AddHeartbeat("BrokerGateway");

// Notifications (Redis based)
builder.Services.AddSingleton<NotificationPublisher>();
builder.Logging.AddRedisNotification();
builder.Services.AddSingleton<ITradingLogger, RedisTradingLogger>();

// Discord Direct Notification (for Startup/System status)
builder.AddDiscordDirectNotification();

// Performance & Trade Tracking
builder.Services.AddSingleton<IPerformanceRepository, yQuant.Infra.Reporting.Repositories.JsonPerformanceRepository>();
builder.Services.AddSingleton<yQuant.Core.Ports.Output.Infrastructure.ITradeRepository, yQuant.Infra.Reporting.Repositories.RedisTradeRepository>();
builder.Services.AddSingleton<yQuant.Core.Ports.Output.Infrastructure.IDailySnapshotRepository, yQuant.Infra.Reporting.Repositories.RedisDailySnapshotRepository>();
// Register KIS Adapter Factory and Adapters
builder.Services.AddSingleton<KISAdapterFactory>();
builder.Services.AddSingleton<Dictionary<string, IBrokerAdapter>>(sp =>
{
    var factory = sp.GetRequiredService<KISAdapterFactory>();
    var adapters = new Dictionary<string, IBrokerAdapter>(StringComparer.OrdinalIgnoreCase);

    foreach (var alias in factory.GetAvailableAccounts())
    {
        var adapter = factory.GetAdapter(alias);
        if (adapter != null)
        {
            adapters[alias] = adapter;
        }
    }
    return adapters;
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Notify systemd that the service is ready (Linux only)
if (OperatingSystem.IsLinux())
{
    try
    {
        // systemd expects READY=1 notification via NOTIFY_SOCKET
        var notifySocket = Environment.GetEnvironmentVariable("NOTIFY_SOCKET");
        if (!string.IsNullOrEmpty(notifySocket))
        {
            using var socket = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.Unix,
                System.Net.Sockets.SocketType.Dgram,
                System.Net.Sockets.ProtocolType.Unspecified);

            var endpoint = new System.Net.Sockets.UnixDomainSocketEndPoint(notifySocket);
            socket.Connect(endpoint);

            var message = System.Text.Encoding.UTF8.GetBytes("READY=1");
            socket.Send(message);

            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Notified systemd that service is ready");
        }
    }
    catch (Exception ex)
    {
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Failed to notify systemd");
    }
}

// Direct Discord Startup Notification
var systemLogger = host.Services.GetService<ISystemLogger>();
if (systemLogger != null)
{
    var appName = "App.BrokerGateway";
    var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
    await systemLogger.LogStartupAsync(appName, version);
}

host.Run();