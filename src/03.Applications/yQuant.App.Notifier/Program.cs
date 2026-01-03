using yQuant.App.Notifier;
using yQuant.App.Notifier.Configuration;
using yQuant.App.Notifier.Services;
using yQuant.Infra.Redis.Extensions;
using yQuant.Infra.Notification.Discord;
using yQuant.Infra.Notification.Telegram;
using yQuant.Core.Ports.Output.Infrastructure;

var settings = new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
};
var builder = Host.CreateApplicationBuilder(settings);

// Load configuration files
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                     .AddJsonFile("appsecrets.json", optional: false, reloadOnChange: true);

// Configure Notifier settings
builder.Services.Configure<NotifierConfiguration>(builder.Configuration.GetSection("Notifier"));

// Register Redis
builder.Services.AddRedisMiddleware(builder.Configuration)
                .AddHeartbeat("Notifier");

// Register Discord Notification Services
builder.AddDiscordDirectNotification();

// Register Telegram Notification Services (optional)
var telegramEnabled = builder.Configuration.GetValue<bool>("Notifier:Telegram:Enabled");
if (telegramEnabled)
{
    builder.Services.AddHttpClient<TelegramNotificationService>();
    builder.Services.AddSingleton<TelegramNotificationService>();
}

// Register MessageRouter
builder.Services.AddSingleton<MessageRouter>();

// Register Worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Notify systemd that the service is ready (Linux only)
if (OperatingSystem.IsLinux())
{
    try
    {
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
    var appName = "App.Notifier";
    var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
    await systemLogger.LogStartupAsync(appName, version);
}

host.Run();
