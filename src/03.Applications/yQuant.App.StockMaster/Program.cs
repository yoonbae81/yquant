using StackExchange.Redis;
using yQuant.App.StockMaster;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Master.KIS;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((hostingContext, config) =>
    {
        config.SetBasePath(AppContext.BaseDirectory);
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        config.AddEnvironmentVariables("yQuant__");
    })
    .ConfigureServices((hostContext, services) =>
    {
        var configuration = hostContext.Configuration;

        // Redis
        var redisConn = hostContext.Configuration["Redis"];
        if (string.IsNullOrEmpty(redisConn))
        {
            throw new InvalidOperationException("Redis connection string is missing. Please set 'Redis' environment variable.");
        }
        
        var options = ConfigurationOptions.Parse(redisConn);
        options.AbortOnConnectFail = false;
        services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(options));
        services.AddSingleton<yQuant.Infra.Middleware.Redis.Interfaces.IRedisService, yQuant.Infra.Middleware.Redis.Services.RedisService>();

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
