using StackExchange.Redis;
using yQuant.App.StockMaster;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Master.KIS;
using yQuant.Infra.Redis.Extensions;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((hostingContext, config) =>
    {
        config.SetBasePath(AppContext.BaseDirectory);
        config.AddJsonFile("sharedsettings.json", optional: false, reloadOnChange: true);
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        config.AddJsonFile($"sharedsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
        config.AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
    })
    .ConfigureServices((hostContext, services) =>
    {
        var configuration = hostContext.Configuration;

        // Redis
        services.AddRedisMiddleware(hostContext.Configuration);

        // HttpClient
        services.AddHttpClient();

        // Core Services - Ports
        services.AddSingleton<IMasterDataLoader, KISMasterDataLoader>();
        services.AddSingleton<IMasterDataRepository, RedisMasterDataRepository>();
        services.AddSingleton<IMasterDataSyncService, MasterDataSyncService>();

        // Register Notification Services
        services.Configure<yQuant.Infra.Notification.Discord.DiscordConfiguration>(hostContext.Configuration.GetSection("Discord"));
        services.AddHttpClient("DiscordWebhook");
        services.AddSingleton<yQuant.Infra.Notification.Discord.Services.DiscordTemplateService>();
        services.AddSingleton<yQuant.Core.Ports.Output.Infrastructure.ISystemLogger, yQuant.Infra.Notification.Discord.DiscordLogger>();

        // Configuration
        services.Configure<StockMasterSettings>(hostContext.Configuration.GetSection("StockMaster"));
        services.AddSingleton(new CommandLineArgs(args));

        // Worker
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
