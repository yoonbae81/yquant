using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using yQuant.App.Console.Commands;
using yQuant.Core.Models;
using yQuant.Infra.Broker.KIS;
using yQuant.Infra.Middleware.Redis;
using yQuant.Infra.Middleware.Redis.Interfaces;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Reporting.Performance.Interfaces;
using yQuant.Infra.Reporting.Performance.Repositories;
using yQuant.Infra.Reporting.Performance.Services;

namespace yQuant.App.Console;

class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length < 2)
        {
            System.Console.WriteLine("Usage: yquant <accountAlias> <command> [args...]");
            System.Console.WriteLine("Commands:");
            System.Console.WriteLine("  auth [-r]");
            System.Console.WriteLine("  deposit");
            System.Console.WriteLine("  positions");
            System.Console.WriteLine("  info <ticker>");
            System.Console.WriteLine("  buy <ticker> <qty> [price]");
            System.Console.WriteLine("  sell <ticker> <qty> [price]");
            System.Console.WriteLine("  report");
            System.Console.WriteLine("  test [-r]");
            return;
        }

        var targetAlias = args[0];
        var cmdName = args[1];

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddEnvironmentVariables();
                config.AddUserSecrets<Program>(optional: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddHttpClient();
                
                // Register KISAdapterFactory
                services.AddSingleton<KISAdapterFactory>();
                services.AddSingleton<IBrokerAdapterFactory>(sp => sp.GetRequiredService<KISAdapterFactory>());

                // Register Account object (using Factory)
                services.AddSingleton<Account>(sp =>
                {
                    var factory = sp.GetRequiredService<KISAdapterFactory>();
                    var account = factory.GetAccount(targetAlias);
                    if (account == null)
                    {
                        throw new InvalidOperationException($"Account '{targetAlias}' not found or invalid.");
                    }
                    return account;
                });

                // Register IBrokerAdapter (using Factory for target alias)
                services.AddSingleton<IBrokerAdapter>(sp =>
                {
                    var factory = sp.GetRequiredService<KISAdapterFactory>();
                    var adapter = factory.GetAdapter(targetAlias);
                    if (adapter == null)
                    {
                        throw new InvalidOperationException($"Failed to create adapter for '{targetAlias}'.");
                    }
                    return adapter;
                });
                // Also register concrete KISBrokerAdapter if needed by some commands (e.g. InfoCommand, OrderCommand used to take it)
                // But better to change them to take IBrokerAdapter. 
                // However, OrderCommand in original code took KISBrokerAdapter. 
                // Let's see if we can cast or just register it if the factory returns it.
                services.AddSingleton<KISBrokerAdapter>(sp => 
                {
                    var adapter = sp.GetRequiredService<IBrokerAdapter>();
                    return (KISBrokerAdapter)adapter;
                });


                services.AddSingleton<IPerformanceRepository, JsonPerformanceRepository>();
                services.AddSingleton<IQuantStatsService, QuantStatsService>();
                services.AddSingleton<yQuant.Core.Services.AssetService>();

                // Register Commands
                // Register Commands
                services.AddTransient<ICommand, AuthCommand>();
                
                services.AddTransient<ICommand>(sp => 
                {
                    return new DepositCommand(sp.GetRequiredService<yQuant.Core.Services.AssetService>(), targetAlias);
                });
                services.AddTransient<ICommand>(sp => 
                {
                    return new PositionsCommand(sp.GetRequiredService<yQuant.Core.Services.AssetService>(), targetAlias);
                });
                services.AddTransient<ICommand>(sp => new InfoCommand(sp.GetRequiredService<KISBrokerAdapter>()));
                services.AddTransient<ICommand>(sp => 
                {
                    var account = sp.GetRequiredService<Account>();
                    return new OrderCommand(sp.GetRequiredService<KISBrokerAdapter>(), sp.GetRequiredService<ILogger<OrderCommand>>(), targetAlias, account.Number, OrderAction.Buy);
                });
                services.AddTransient<ICommand>(sp => 
                {
                    var account = sp.GetRequiredService<Account>();
                    return new OrderCommand(sp.GetRequiredService<KISBrokerAdapter>(), sp.GetRequiredService<ILogger<OrderCommand>>(), targetAlias, account.Number, OrderAction.Sell);
                });
                services.AddTransient<ICommand>(sp => new ReportCommand(sp.GetRequiredService<IPerformanceRepository>(), sp.GetRequiredService<IQuantStatsService>(), targetAlias));
                services.AddTransient<ICommand, TestCommand>();

                services.AddSingleton<CommandRouter>();
            })
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        
        // Check if Account can be resolved (validates existence)
        try 
        {
            var account = host.Services.GetService<Account>();
            if (account == null)
            {
                 // Should be caught by GetService throwing or returning null if not required, but we used AddSingleton factory which throws.
                 // However, GetService might catch the exception inside? No, GetService returns null if not found, but if factory throws, it throws.
                 // Let's use GetService and let it throw if factory fails?
                 // Actually, let's just try to resolve it.
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error: {ex.Message}");
            return;
        }

        // Check if services are registered (if ConfigureServices returned early)
        var adapter = host.Services.GetService<KISBrokerAdapter>();
        if (adapter == null)
        {
            System.Console.WriteLine("Error: Failed to initialize services. Check credentials.");
            return;
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
}
