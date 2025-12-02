using StackExchange.Redis;
using yQuant.App.StockMaster;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Master.KIS;
using yQuant.Infra.Redis.Extensions;

using yQuant.Core.Utils;

EnvValidator.Validate();

var settings = new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
};
var builder = Host.CreateApplicationBuilder(settings);

builder.Configuration.AddJsonFile("sharedsettings.json", optional: false, reloadOnChange: true)
                     .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                     .AddJsonFile($"sharedsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                     .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

// Redis
builder.Services.AddRedisMiddleware(builder.Configuration);

// HttpClient
builder.Services.AddHttpClient();

// Core Services - Ports
builder.Services.AddSingleton<IMasterDataLoader, KISMasterDataLoader>();
builder.Services.AddSingleton<IMasterDataRepository, RedisMasterDataRepository>();
builder.Services.AddSingleton<IMasterDataSyncService, MasterDataSyncService>();

// Register Notification Services
builder.Services.Configure<yQuant.Infra.Notification.Discord.DiscordConfiguration>(builder.Configuration.GetSection("Discord"));
builder.Services.AddHttpClient("DiscordWebhook");
builder.Services.AddSingleton<yQuant.Infra.Notification.Discord.Services.DiscordTemplateService>();
builder.Services.AddSingleton<yQuant.Core.Ports.Output.Infrastructure.ISystemLogger, yQuant.Infra.Notification.Discord.DiscordLogger>();

// Configuration
builder.Services.Configure<StockMasterSettings>(builder.Configuration.GetSection("StockMaster"));
builder.Services.AddSingleton(new CommandLineArgs(args));

// Worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

await host.RunAsync();
