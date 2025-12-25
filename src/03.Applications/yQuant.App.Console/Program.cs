using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using yQuant.App.Console.Commands;
using yQuant.Core.Models;
using yQuant.Infra.Broker.KIS;
using yQuant.Infra.Redis.Extensions;
using yQuant.Infra.Redis.Interfaces;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Reporting.Repositories;
using StackExchange.Redis;
using yQuant.Infra.Redis.Adapters;


namespace yQuant.App.Console;

class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length < 1)
        {
            PrintUsage();
            return;
        }

        var cmdName = args[0];

        // Commands that don't require an account
        var noAccountCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "catalog",
            "checkaccounts",
            "info",
            "help"
        };

        bool needsAccount = !noAccountCommands.Contains(cmdName);
        string targetAccount = "N/A";

        if (needsAccount)
        {
            if (args.Length < 2)
            {
                System.Console.WriteLine($"Error: Command '{cmdName}' requires an account name.");
                PrintUsage();
                return;
            }
            targetAccount = args[1];
        }

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                      .AddJsonFile("appsecrets.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                // Redis Connection
                services.AddRedisMiddleware(context.Configuration);

                // Register RedisBrokerClient
                services.AddSingleton<RedisBrokerClient>(sp =>
                    new RedisBrokerClient(sp.GetRequiredService<IConnectionMultiplexer>(), targetAccount));

                services.AddSingleton<IBrokerAdapter>(sp => sp.GetRequiredService<RedisBrokerClient>());

                // Notification Services (required by Catalog sync)
                services.Configure<yQuant.Infra.Notification.Discord.DiscordConfiguration>(context.Configuration.GetSection("Notifier:Discord"));
                services.AddHttpClient("DiscordWebhook");
                services.AddSingleton<yQuant.Infra.Notification.Discord.Services.DiscordTemplateService>();
                services.AddSingleton<ISystemLogger, yQuant.Infra.Notification.Discord.DiscordLogger>();

                // Catalog Services (Stock Catalog Sync)
                services.AddHttpClient();
                services.AddSingleton<yQuant.App.Console.Services.StockCatalogLoader>();
                services.AddSingleton<yQuant.App.Console.Services.StockCatalogRepository>();
                services.AddSingleton<yQuant.App.Console.Services.StockCatalogSyncService>();
                // Configure CatalogSettings by binding the Console section from catalog.json
                services.Configure<CatalogSettings>(context.Configuration.GetSection("Console:Catalog"));

                // Register Commands
                services.AddTransient<ICommand>(sp =>
                {
                    return new DepositCommand(sp.GetRequiredService<RedisBrokerClient>());
                });
                services.AddTransient<ICommand>(sp =>
                {
                    return new PositionsCommand(sp.GetRequiredService<RedisBrokerClient>());
                });
                services.AddTransient<ICommand>(sp => new InfoCommand(sp.GetRequiredService<IBrokerAdapter>()));
                services.AddTransient<ICommand>(sp =>
                {
                    return new OrderCommand(sp.GetRequiredService<IBrokerAdapter>(), sp.GetRequiredService<ILogger<OrderCommand>>(), targetAccount, targetAccount, OrderAction.Buy);
                });
                services.AddTransient<ICommand>(sp =>
                {
                    return new OrderCommand(sp.GetRequiredService<IBrokerAdapter>(), sp.GetRequiredService<ILogger<OrderCommand>>(), targetAccount, targetAccount, OrderAction.Sell);
                });
                services.AddTransient<ICommand>(sp => new CheckAccountsCommand(sp.GetRequiredService<IConnectionMultiplexer>()));
                services.AddTransient<ICommand, CatalogCommand>();

                services.AddSingleton<CommandRouter>();
            })
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        // Startup Check (only if account is required)
        if (needsAccount)
        {
            var client = host.Services.GetRequiredService<RedisBrokerClient>();
            var pingResult = await client.PingAsync();
            if (!pingResult.Success)
            {
                System.Console.WriteLine($"Error: {pingResult.Message}");
                return;
            }
        }

        try
        {
            var router = host.Services.GetRequiredService<CommandRouter>();
            await router.ExecuteAsync(cmdName, args);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during execution.");
        }
    }

    private static void PrintUsage()
    {
        System.Console.WriteLine("Usage: yquant <command> [account] [args...]");
        System.Console.WriteLine("\nCommands that require account:");
        System.Console.WriteLine("  deposit <account> <currency> [-r]");
        System.Console.WriteLine("  positions <account> <country> [-r]");
        System.Console.WriteLine("  buy <account> <ticker> <qty> [price]");
        System.Console.WriteLine("  sell <account> <ticker> <qty> [price]");
        System.Console.WriteLine("\nCommands that don't require account:");
        System.Console.WriteLine("  catalog [country]  - Sync stock catalog data");
        System.Console.WriteLine("  checkaccounts       - List registered accounts in Redis");
        System.Console.WriteLine("  info <ticker>       - Check price for a ticker");
    }
}

