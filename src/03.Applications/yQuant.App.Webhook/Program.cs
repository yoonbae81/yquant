using yQuant.App.Webhook.Models;
using yQuant.Core.Models;
using StackExchange.Redis;
using System.Net;
using System.Text.Json;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Notification;
using yQuant.Infra.Notification.Discord;
using yQuant.Infra.Redis.Extensions;

var builder = WebApplication.CreateSlimBuilder(args);

// Configure Kestrel to listen on port 6000
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(6000);
});

// Configure JSON serialization for minimal API
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

// Find the configuration directory (climb up to find appsettings.json)
var configDir = Directory.GetCurrentDirectory();
while (configDir != null && !File.Exists(Path.Combine(configDir, "appsettings.json")))
{
    configDir = Path.GetDirectoryName(configDir);
}
configDir ??= AppContext.BaseDirectory;

// Load configuration files
builder.Configuration.SetBasePath(configDir)
                     .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                     .AddJsonFile("appsecrets.json", optional: false, reloadOnChange: true);

// Register Redis Middleware (uses AddRedisMiddleware from yQuant.Infra.Redis)
builder.Services.AddRedisMiddleware(builder.Configuration)
                .AddHeartbeat("Webhook");

// Register Notification Services (Redis based)
builder.Services.AddSingleton<NotificationPublisher>();
builder.Services.AddSingleton<ITradingLogger, RedisTradingLogger>();
builder.Services.AddSingleton<ISystemLogger, RedisSystemLogger>();

// Discord Direct Notification (for Startup/System status)
builder.AddDiscordDirectNotification();

var app = builder.Build();

// Health Check Endpoint
app.MapGet("/health", async (IConnectionMultiplexer redis) =>
{
    try
    {
        var db = redis.GetDatabase();
        await db.PingAsync();
        return Results.Ok(new { Status = "Healthy", Redis = "Connected", Timestamp = DateTime.UtcNow, Service = "yQuant.App.Webhook" });
    }
    catch (Exception ex)
    {
        return Results.Json(new { Status = "Unhealthy", Redis = "Disconnected", Error = ex.Message }, statusCode: 503);
    }
});

// Logging Middleware
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var sw = System.Diagnostics.Stopwatch.StartNew();

    try
    {
        await next();
    }
    finally
    {
        sw.Stop();
        logger.LogInformation(
            "Request {Method} {Path} responded {StatusCode} in {Elapsed:0.0000}ms. IP: {RemoteIpAddress}",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            sw.Elapsed.TotalMilliseconds,
            context.Connection.RemoteIpAddress);
    }
});

// IP Whitelisting Middleware
app.Use(async (context, next) =>
{
    var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
    var allowedIps = configuration.GetSection("Webhook:AllowedIps").Get<string[]>()
                     ?? [];
    var remoteIpAddress = context.Connection.RemoteIpAddress;

    // Allow localhost for development/testing
    if (remoteIpAddress == null || IPAddress.IsLoopback(remoteIpAddress))
    {
        await next();
        return;
    }

    if (!allowedIps.Any(ip => IPAddress.Parse(ip).Equals(remoteIpAddress)))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsync("Forbidden: IP address not allowed.");
        return;
    }

    await next();
});

// Webhook Endpoint
app.MapPost("/webhook", async (HttpContext context, TradingViewPayload payload, IConfiguration configuration, IConnectionMultiplexer redis, ITradingLogger tradingLogger, ISystemLogger systemLogger) =>
{
    // Security Validation - Secret Key
    var webhookSecret = configuration["Webhook:Secrets:TradingView"];
    if (string.IsNullOrEmpty(webhookSecret) || payload.Secret != webhookSecret)
    {
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var receivedSecret = payload.Secret ?? "(null)";
        var errorContext = $"Webhook Security Warning (IP: {remoteIp})";
        var errorMessage = $"Invalid Webhook Secret. Received: '{receivedSecret}'. This could be an incorrect configuration or a hacking attempt.";

        // Notify Discord System:Error channel
        _ = systemLogger.LogSystemErrorAsync(errorContext, new UnauthorizedAccessException(errorMessage));

        return Results.Unauthorized();
    }

    // Data Normalization: TradingViewPayload to Signal
    if (payload.Ticker == null || payload.Action == null || payload.Exchange == null || payload.Strategy == null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return Results.BadRequest("Missing required fields in payload.");
    }

    if (!Enum.TryParse(payload.Action, true, out OrderAction orderAction))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return Results.BadRequest($"Invalid Action value: {payload.Action}.");
    }

    CurrencyType? currencyType = null;
    if (payload.Currency != null && Enum.TryParse(payload.Currency, true, out CurrencyType parsedCurrencyType))
    {
        currencyType = parsedCurrencyType;
    }

    var signal = new Signal
    {
        Id = Guid.NewGuid(),
        Ticker = payload.Ticker,
        Exchange = payload.Exchange,
        Currency = currencyType,
        Action = orderAction,
        Price = payload.Price,
        Strength = (int?)payload.Strength,
        Strategy = payload.Strategy,
        Timestamp = DateTime.UtcNow
    };

    // Fire-and-forget logging
    _ = tradingLogger.LogSignalAsync(signal);

    // Message Publishing to Redis
    var db = redis.GetDatabase();
    var signalJson = JsonSerializer.Serialize(signal);
    await db.PublishAsync(RedisChannel.Literal("signal"), signalJson);

    return Results.Ok("Signal received and published.");
});

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

            app.Logger.LogInformation("Notified systemd that service is ready");
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Failed to notify systemd");
    }
}

// Direct Discord Startup Notification
var systemLoggerService = app.Services.GetService<ISystemLogger>();
if (systemLoggerService != null)
{
    var appName = "App.Webhook";
    var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
    await systemLoggerService.LogStartupAsync(appName, version);
}

app.Run();

// JSON serialization context for Native AOT support
[System.Text.Json.Serialization.JsonSerializable(typeof(TradingViewPayload))]
[System.Text.Json.Serialization.JsonSerializable(typeof(Signal))]
internal partial class AppJsonSerializerContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
