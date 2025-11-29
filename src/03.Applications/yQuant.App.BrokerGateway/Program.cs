using yQuant.App.BrokerGateway;
using yQuant.Infra.Middleware.Redis.Extensions;
using StackExchange.Redis;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Broker.KIS;
using yQuant.Infra.Notification.Telegram;
using yQuant.Infra.Notification.Discord;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration; // Needed for GetConnectionString
using Microsoft.Extensions.Logging;
using yQuant.Infra.Middleware.Redis.Interfaces;

var builder = Host.CreateApplicationBuilder(args);

// Redis configuration removed as per user request
// var redisConn = Environment.GetEnvironmentVariable("Redis");
// if (!string.IsNullOrEmpty(redisConn))
// {
//     builder.Configuration.AddRedis(redisConn);
// }

// Register Redis
// Register Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var redisConn = Environment.GetEnvironmentVariable("Redis");
    if (string.IsNullOrEmpty(redisConn))
    {
        throw new InvalidOperationException("Redis connection string is missing. Please set 'Redis' environment variable.");
    }

    return ConnectionMultiplexer.Connect(redisConn);
});

// Register Telegram Notification Service
builder.Services.AddHttpClient<INotificationService, TelegramNotificationService>();
builder.Services.AddSingleton<TelegramMessageBuilder>();

// Register KIS HttpClient
builder.Services.AddHttpClient("KIS");
builder.Services.AddRedisMiddleware(builder.Configuration);

// Register KISAccountManager - manages multiple KIS accounts with independent credentials
// Register KISAccountManager - manages multiple KIS accounts with independent credentials
builder.Services.AddSingleton<KISAccountManager>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<KISAccountManager>>();
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var redisService = sp.GetRequiredService<IRedisService>();
    var config = sp.GetRequiredService<IConfiguration>();
    
    var manager = new KISAccountManager(logger, httpClientFactory, sp, redisService);
    
    // Reverted to single account from KIS section
    var kisConfig = config.GetSection("KIS");
    var userId = kisConfig["UserId"];
    var appKey = kisConfig["AppKey"];
    var appSecret = kisConfig["AppSecret"];
    var accountNumber = kisConfig["AccountNumber"];
    var baseUrl = kisConfig["BaseUrl"] ?? "https://openapi.koreainvestment.com:9443";

    if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(appKey) && !string.IsNullOrEmpty(appSecret) && !string.IsNullOrEmpty(accountNumber))
    {
        // Use "Default" or similar as alias for single account mode
        manager.RegisterAccount("Default", appKey, appSecret, baseUrl, accountNumber);
    }
    else
    {
        logger.LogWarning("Incomplete KIS configuration in appsettings.json.");
    }
    
    return manager;
});

// Register Discord Notification Services
builder.Services.Configure<DiscordConfiguration>(builder.Configuration.GetSection("Discord"));
builder.Services.AddHttpClient("DiscordWebhook");
builder.Services.AddSingleton<ITradingLogger, DiscordLogger>();
builder.Services.AddSingleton<IPerformanceRepository, yQuant.Infra.Reporting.Performance.Repositories.JsonPerformanceRepository>();
builder.Services.AddSingleton<ITradingLogger, yQuant.Infra.Reporting.Performance.Loggers.PersistenceTradingLogger>();
builder.Services.AddSingleton<ISystemLogger, DiscordLogger>();
builder.Services.AddSingleton<yQuant.Infra.Notification.Common.Services.TemplateService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();