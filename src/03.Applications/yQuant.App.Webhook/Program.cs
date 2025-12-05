using yQuant.App.TradingViewWebhook.Models;
using yQuant.Core.Models;
using StackExchange.Redis;
using System.Net;
using System.Text.Json;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Notification.Discord;
using yQuant.Infra.Redis.Extensions;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Configuration.AddJsonFile("sharedsettings.json", optional: false, reloadOnChange: true)
                     .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                     .AddJsonFile($"sharedsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                     .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddRedisMiddleware(builder.Configuration)
                .AddHeartbeat("Webhook");

// Register Discord Notification Services
builder.Services.Configure<DiscordConfiguration>(builder.Configuration.GetSection("Discord"));
builder.Services.AddHttpClient("DiscordWebhook");
builder.Services.AddSingleton<yQuant.Infra.Notification.Discord.Services.DiscordTemplateService>();
builder.Services.AddSingleton<ITradingLogger, DiscordLogger>();
builder.Services.AddSingleton<ISystemLogger, DiscordLogger>();

var app = builder.Build();

// Configure the HTTP request pipeline.

// Health Check Endpoint
app.MapGet("/health", async (IConnectionMultiplexer redis) =>
{
    try
    {
        var db = redis.GetDatabase();
        await db.PingAsync();
        return Results.Ok(new { Status = "Healthy", Redis = "Connected", Timestamp = DateTime.UtcNow });
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
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Request {Method} {Path} responded {StatusCode} in {Elapsed:0.0000}ms. IP: {RemoteIpAddress}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                sw.Elapsed.TotalMilliseconds,
                context.Connection.RemoteIpAddress);
        }
    }
});

// IP Whitelisting Middleware
app.Use(async (context, next) =>
{
    var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
    var allowedIps = configuration.GetSection("Security:AllowedIps").Get<string[]>() ?? [];
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
});

app.MapPost("/webhook", async (HttpContext context, TradingViewPayload payload, IConfiguration configuration, IConnectionMultiplexer redis, ITradingLogger tradingLogger) =>
{
    // Security Validation - Secret Key
    var webhookSecret = configuration["Security:WebhookSecret"];
    if (string.IsNullOrEmpty(webhookSecret) || payload.Secret != webhookSecret)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Results.Unauthorized();
    }

    // Data Normalization: TradingViewPayload to Signal
    if (payload.Ticker == null || payload.Action == null || payload.Exchange == null || payload.Comment == null)
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
        Source = payload.Comment,
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

app.Run();
