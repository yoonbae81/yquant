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
using yQuant.Core.Models; // Added for Account

var builder = Host.CreateApplicationBuilder(args);

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
using yQuant.Core.Models; // Added for Account

var builder = Host.CreateApplicationBuilder(args);

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

builder.Services.AddSingleton<yQuant.Infra.Notification.Discord.Services.DiscordTemplateService>();
builder.Services.AddSingleton<yQuant.Infra.Notification.Telegram.Services.TelegramTemplateService>();
builder.Services.AddSingleton<IPerformanceRepository, yQuant.Infra.Reporting.Performance.Repositories.JsonPerformanceRepository>();
builder.Services.AddSingleton<ITradingLogger, yQuant.Infra.Reporting.Performance.Loggers.PersistenceTradingLogger>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();