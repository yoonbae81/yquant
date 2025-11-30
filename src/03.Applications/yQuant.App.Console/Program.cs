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

        var root = Directory.GetCurrentDirectory();
        var dotenv = Path.Combine(root, ".env");
        var dotenvLocal = Path.Combine(root, ".env.local");

        // Try to find .env in parent directories if not found in current (common in dev)
        if (!File.Exists(dotenv) && !File.Exists(dotenvLocal))
        {
            var parent = Directory.GetParent(root);
            while (parent != null)
            {
                var pEnv = Path.Combine(parent.FullName, ".env");
                var pEnvLocal = Path.Combine(parent.FullName, ".env.local");
                if (File.Exists(pEnv) || File.Exists(pEnvLocal))
                {
                    root = parent.FullName;
                    dotenv = Path.Combine(root, ".env");
                    dotenvLocal = Path.Combine(root, ".env.local");
                    break;
                }
                parent = parent.Parent;
            }
        }

        LoadEnvFile(dotenv);
        LoadEnvFile(dotenvLocal);

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddEnvironmentVariables();
                config.AddUserSecrets<Program>(optional: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddHttpClient();
                services.AddSingleton<KISAccountProvider>();

                // Register Account object via Provider
                services.AddSingleton<Account>(sp =>
                {
                    var provider = sp.GetRequiredService<KISAccountProvider>();
                    var account = provider.GetAccount(targetAlias);
                    if (account == null)
                    {
                        throw new InvalidOperationException($"Account '{targetAlias}' not found or invalid.");
                    }
                    return account;
                });

                // Register KISApiConfig
                services.AddSingleton<KISApiConfig>(sp =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var apiConfig = KISApiConfig.Load(Path.Combine(AppContext.BaseDirectory, "API"));
                    
                    return apiConfig;
                });

                // Register KISClient with Account object
                services.AddSingleton<IKISClient>(sp =>
                {
                    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(KISClient));
                    var logger = sp.GetRequiredService<ILogger<KISClient>>();
                    var apiConfig = sp.GetRequiredService<KISApiConfig>();
                    var account = sp.GetRequiredService<Account>();
                    
                    return new KISClient(httpClient, logger, account, apiConfig);
                });

                services.AddSingleton<KISBrokerAdapter>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<KISBrokerAdapter>>();
                    var client = sp.GetRequiredService<IKISClient>();
                    return new KISBrokerAdapter(client, logger);
                });
                services.AddSingleton<IBrokerAdapter>(sp => sp.GetRequiredService<KISBrokerAdapter>());

                services.AddSingleton<IPerformanceRepository, JsonPerformanceRepository>();
                services.AddSingleton<IQuantStatsService, QuantStatsService>();
                services.AddSingleton<yQuant.Core.Services.AssetService>();

                // Register Commands
                services.AddTransient<ICommand, AuthCommand>();
                services.AddTransient<ICommand>(sp => 
                {
                    var account = sp.GetRequiredService<Account>();
                    return new DepositCommand(sp.GetRequiredService<yQuant.Core.Services.AssetService>(), account.Number);
                });
                services.AddTransient<ICommand>(sp => 
                {
                    var account = sp.GetRequiredService<Account>();
                    return new PositionsCommand(sp.GetRequiredService<yQuant.Core.Services.AssetService>(), account.Number);
                });
                services.AddTransient<ICommand>(sp => new PriceCommand(sp.GetRequiredService<KISBrokerAdapter>()));
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

    static void LoadEnvFile(string filePath)
    {
        if (!File.Exists(filePath)) return;

        foreach (var line in File.ReadAllLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

            var parts = line.Split('=', 2);
            if (parts.Length != 2) continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();
            
            // Handle double quotes
            if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2)
            {
                value = value.Substring(1, value.Length - 2);
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
