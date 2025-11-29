using StackExchange.Redis;
using yQuant.App.StockMaster;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Master.KIS;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        var configuration = hostContext.Configuration;

        // Redis
        var redisConn = Environment.GetEnvironmentVariable("Redis");
        if (string.IsNullOrEmpty(redisConn))
        {
            throw new InvalidOperationException("Redis connection string is missing. Please set 'Redis' environment variable.");
        }
        
        var options = ConfigurationOptions.Parse(redisConn);
        options.AbortOnConnectFail = false;
        services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(options));

        // HttpClient
        services.AddHttpClient();

        // Core Services - Ports
        services.AddSingleton<IMasterDataLoader, KISMasterDataLoader>();
        services.AddSingleton<IMasterDataRepository, RedisMasterDataRepository>();
        services.AddSingleton<IMasterDataSyncService, MasterDataSyncService>();

        // Register Notification Services
        services.Configure<yQuant.Infra.Notification.Discord.DiscordConfiguration>(hostContext.Configuration.GetSection("Discord"));
        services.AddHttpClient("DiscordWebhook");
        services.AddSingleton<yQuant.Infra.Notification.Common.Services.TemplateService>();
        services.AddSingleton<yQuant.Core.Ports.Output.Infrastructure.ISystemLogger, yQuant.Infra.Notification.Discord.DiscordLogger>();

        // Configuration
        services.Configure<StockMasterSettings>(hostContext.Configuration.GetSection("StockMaster"));
        services.AddSingleton(new CommandLineArgs(args));

        // Worker
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
