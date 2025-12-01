using yQuant.App.BrokerGateway;
using yQuant.Infra.Redis.Extensions;
using StackExchange.Redis;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Broker.KIS;
using yQuant.Infra.Notification.Telegram;
using yQuant.Infra.Notification.Discord;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using yQuant.Infra.Redis.Interfaces;
using yQuant.Core.Models;

var builder = Host.CreateApplicationBuilder(args);

// Register Telegram Notification Service
builder.Services.AddHttpClient<INotificationService, TelegramNotificationService>();
builder.Services.AddSingleton<TelegramMessageBuilder>();

// Register KIS HttpClient
builder.Services.AddHttpClient("KIS");
builder.Services.AddRedisMiddleware(builder.Configuration);

builder.Services.AddSingleton<yQuant.Infra.Notification.Discord.Services.DiscordTemplateService>();
builder.Services.AddSingleton<yQuant.Infra.Notification.Telegram.Services.TelegramTemplateService>();
builder.Services.AddSingleton<IPerformanceRepository, yQuant.Infra.Reporting.Performance.Repositories.JsonPerformanceRepository>();
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
host.Run();