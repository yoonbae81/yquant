using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using yQuant.App.OrderManager;
using yQuant.App.OrderManager.Adapters;
using yQuant.Core.Extensions;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Core.Ports.Output.Policies;
using yQuant.Infra.Valkey.Extensions;
using yQuant.Infra.Notification.Discord;
using yQuant.Infra.Persistence;




// Find the configuration directory (climb up to find appsettings.json)
var configDir = Directory.GetCurrentDirectory();
while (configDir != null && !File.Exists(Path.Combine(configDir, "appsettings.json")))
{
    configDir = Path.GetDirectoryName(configDir);
}
configDir ??= AppContext.BaseDirectory;

var settings = new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = configDir
};
var builder = Host.CreateApplicationBuilder(settings);

// Load configuration files
builder.Configuration.SetBasePath(configDir)
                     .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                     .AddJsonFile("appsecrets.json", optional: false, reloadOnChange: true);



builder.Services.AddValkeyMiddleware(builder.Configuration)
                .AddHeartbeat("OrderManager");

// Register Notification Services
builder.Services.AddSingleton<yQuant.Infra.Notification.NotificationPublisher>();

// Discord Direct Notification (for Startup/System status)
builder.AddDiscordDirectNotification();

// Register Core Services (exclude ManualTradingService as OrderManager doesn't need direct broker interaction)
builder.Services.AddyQuantCore(includeManualTrading: false);

// Register Infrastructure Adapters
builder.Services.AddSingleton<IAccountRepository, ValkeyAccountRepository>();
builder.Services.AddSingleton<IStrategyAccountMapper, ConfigStrategyAccountMapper>();
builder.Services.AddSingleton<IStrategyPolicyMapper, ConfigStrategyPolicyMapper>();
builder.Services.AddSingleton<IOrderPublisher, ValkeyOrderPublisher>();

// Register Performance Tracking
// IDailySnapshotRepository is registered via AddMariaDbPersistence() below

// Register Schedule Executor
builder.Services.AddSingleton<yQuant.App.OrderManager.Services.ScheduleExecutor>();

// Register Daily Snapshot Service
builder.Services.AddHostedService<yQuant.App.OrderManager.Services.DailySnapshotService>();

// Register Policies (Fixed Implementation)
builder.Services.AddSingleton<IPositionSizer>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var sizingConfig = config.GetSection("OrderManager:Policies:Sizing:Basic");

    if (!sizingConfig.Exists())
    {
        throw new InvalidOperationException("Basic Position Sizer policy is not configured under OrderManager:Policies:Sizing:Basic.");
    }

    // Directly instantiate using DI for dependencies if any, and passing settings
    return ActivatorUtilities.CreateInstance<yQuant.Policies.Sizing.BasicPositionSizer>(sp, sizingConfig);
});

builder.Services.AddSingleton<IPositionSizer, yQuant.Policies.Sizing.OnlyOnePositionSizer>();

builder.Services.AddSingleton<IEnumerable<IMarketRule>>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<Program>>();
    var marketsConfig = config.GetSection("Markets").GetChildren();
    var rules = new List<IMarketRule>();

    foreach (var marketConfig in marketsConfig)
    {
        try
        {
            var rule = new yQuant.Core.Policies.ConfigurableMarketRule(marketConfig);
            rules.Add(rule);
            logger.LogInformation("Loaded market rule for: {MarketName}", marketConfig.Key);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load Market Rule for: {MarketName}", marketConfig.Key);
        }
    }

    if (rules.Count == 0)
    {
        logger.LogWarning("No market rules were loaded. Check Markets configuration.");
    }

    return rules;
});

builder.Services.AddHostedService<Worker>();

// Persistence & Archiving
builder.Services.AddMariaDbPersistence(builder.Configuration);
builder.Services.AddTradeArchiver();

var host = builder.Build();

// Initialize MariaDB Schema
await host.Services.InitializeMariaDbPersistenceAsync();

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
    var appName = "OrderManager";
    var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
    await systemLogger.LogStartupAsync(appName, version);
}

host.Run();

// Helper class for logger resolution in Program (not strictly needed if we use sp inside factory)
public partial class Program { }
