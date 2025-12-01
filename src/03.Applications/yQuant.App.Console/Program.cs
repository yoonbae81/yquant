
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
using StackExchange.Redis;
using yQuant.App.Console.Services;

namespace yQuant.App.Console;

class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length < 2)
        {
            System.Console.WriteLine("Usage: yquant <account> <command> [args...]");
            System.Console.WriteLine("Commands:");
            System.Console.WriteLine("  deposit <currency> [-r]");
            System.Console.WriteLine("  positions <country> [-r]");
            System.Console.WriteLine("  info <ticker>");
            System.Console.WriteLine("  buy <ticker> <qty> [price]");
            System.Console.WriteLine("  sell <ticker> <qty> [price]");
            return;
        }

        var targetAccount = args[0];
        var cmdName = args[1];

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddEnvironmentVariables();
                config.AddUserSecrets<Program>(optional: true);
            })
            .ConfigureServices((context, services) =>
            {
                // Redis Connection
                services.AddSingleton<IConnectionMultiplexer>(sp =>
                {
                    var redisConn = Environment.GetEnvironmentVariable("Redis");
                    if (string.IsNullOrEmpty(redisConn))
                    {
                        throw new InvalidOperationException("Redis connection string is missing.");
                    }
                    return ConnectionMultiplexer.Connect(redisConn);
                });

                // Register RedisBrokerClient
                services.AddSingleton<RedisBrokerClient>(sp => 
                    new RedisBrokerClient(sp.GetRequiredService<IConnectionMultiplexer>(), targetAccount));
                
                services.AddSingleton<IBrokerAdapter>(sp => sp.GetRequiredService<RedisBrokerClient>());

                // Register Commands
                services.AddTransient<ICommand>(sp => 
                {
                    return new DepositCommand(sp.GetRequiredService<RedisBrokerClient>());
                });
                services.AddTransient<ICommand>(sp => 
                {
                    return new PositionsCommand(sp.GetRequiredService<RedisBrokerClient>());
                });
                services.AddTransient<ICommand>(sp => new InfoCommand(sp.GetRequiredService<IBrokerAdapter>())); // InfoCommand needs update? It uses IBrokerAdapter.
                services.AddTransient<ICommand>(sp => 
                {
                    // OrderCommand needs Account Number? 
                    // RedisBrokerClient doesn't have Account Number. 
                    // But OrderCommand only needs it to pass to Order object.
                    // The Gateway will look up the real account.
                    // So we can pass a dummy or the alias as number for now, or update OrderCommand.
                    // Let's check OrderCommand.
                    return new OrderCommand(sp.GetRequiredService<IBrokerAdapter>(), sp.GetRequiredService<ILogger<OrderCommand>>(), targetAccount, targetAccount, OrderAction.Buy);
                });
                services.AddTransient<ICommand>(sp => 
                {
                    return new OrderCommand(sp.GetRequiredService<IBrokerAdapter>(), sp.GetRequiredService<ILogger<OrderCommand>>(), targetAccount, targetAccount, OrderAction.Sell);
                });

                services.AddSingleton<CommandRouter>();
            })
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        
        // Startup Check
        var client = host.Services.GetRequiredService<RedisBrokerClient>();
        var pingResult = await client.PingAsync();
        if (!pingResult.Success)
        {
            System.Console.WriteLine($"Error: {pingResult.Message}");
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
