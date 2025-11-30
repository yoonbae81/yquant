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
            System.Console.WriteLine("  token [-r]");
            System.Console.WriteLine("  deposit");
            System.Console.WriteLine("  positions");
            System.Console.WriteLine("  info <ticker>");
            System.Console.WriteLine("  buy <ticker> <qty> [price]");
            System.Console.WriteLine("  sell <ticker> <qty> [price]");
            System.Console.WriteLine("  report");
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
                // Read from multi-account configuration
                var accountsSection = context.Configuration.GetSection("Accounts");
                var targetAccount = accountsSection.GetChildren()
                    .FirstOrDefault(a => a["Alias"]?.Equals(targetAlias, StringComparison.OrdinalIgnoreCase) == true);
                
                if (targetAccount == null)
                {
                    // Will be handled in Main after build
                    return; 
                }

                var accountNo = targetAccount["Number"];
                var appKey = targetAccount["AppKey"];
                var appSecret = targetAccount["AppSecret"];
                var baseUrl = targetAccount["BaseUrl"] ?? "https://openapi.koreainvestment.com:9443";

                if (string.IsNullOrEmpty(appKey) || string.IsNullOrEmpty(appSecret))
                {
                    System.Console.WriteLine($"CRITICAL: Account '{targetAlias}' is missing credentials (AppKey, AppSecret).");
                    return;
                }

                services.AddHttpClient<IKISConnector, KISConnector>(client =>
                {
                    client.BaseAddress = new Uri(baseUrl);
                });

                // Create Account object
                var account = new Account
                {
                    Alias = targetAlias,
                    Number = accountNo!,
                    Broker = targetAccount["Broker"] ?? "KIS",
                    AppKey = appKey!,
                    AppSecret = appSecret!,
                    Deposits = new Dictionary<CurrencyType, decimal>(),
                    Active = true
                };

                // Register KISApiConfig
                services.AddSingleton<KISApiConfig>(sp =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var apiConfig = KISApiConfig.Load(Path.Combine(AppContext.BaseDirectory, "API"));
                    
                    return apiConfig;
                });

                // Register KISConnector with Account object
                services.AddSingleton<IKISConnector>(sp =>
                {
                    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(KISConnector));
                    var logger = sp.GetRequiredService<ILogger<KISConnector>>();
                    var redis = sp.GetService<IRedisService>();
                    var apiConfig = sp.GetRequiredService<KISApiConfig>();
                    
                    // Override BaseUrl from account config if present
                    if (!string.IsNullOrEmpty(baseUrl))
                    {
                        apiConfig.BaseUrl = baseUrl;
                    }

                    return new KISConnector(httpClient, logger, redis, account, apiConfig);
                });

                services.AddSingleton<KISBrokerAdapter>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<KISBrokerAdapter>>();
                    var client = sp.GetRequiredService<IKISConnector>();
                    var prefix = accountNo?.Length >= 8 ? accountNo.Substring(0, 8) : "00000000";
                    return new KISBrokerAdapter(logger, client, prefix, targetAlias);
                });

                services.AddSingleton<IPerformanceRepository, JsonPerformanceRepository>();
                services.AddSingleton<IQuantStatsService, QuantStatsService>();
                services.AddSingleton<yQuant.Core.Services.AssetService>();

                // Register Commands
                services.AddTransient<ICommand, TokenCommand>();
                services.AddTransient<ICommand>(sp => new DepositCommand(sp.GetRequiredService<yQuant.Core.Services.AssetService>(), accountNo));
                services.AddTransient<ICommand>(sp => new PositionsCommand(sp.GetRequiredService<yQuant.Core.Services.AssetService>(), accountNo));
                services.AddTransient<ICommand>(sp => new PriceCommand(sp.GetRequiredService<KISBrokerAdapter>()));
                services.AddTransient<ICommand>(sp => new OrderCommand(sp.GetRequiredService<KISBrokerAdapter>(), sp.GetRequiredService<ILogger<OrderCommand>>(), targetAlias, accountNo, OrderAction.Buy));
                services.AddTransient<ICommand>(sp => new OrderCommand(sp.GetRequiredService<KISBrokerAdapter>(), sp.GetRequiredService<ILogger<OrderCommand>>(), targetAlias, accountNo, OrderAction.Sell));
                services.AddTransient<ICommand>(sp => new ReportCommand(sp.GetRequiredService<IPerformanceRepository>(), sp.GetRequiredService<IQuantStatsService>(), targetAlias));

                services.AddSingleton<CommandRouter>();
            })
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var config = host.Services.GetRequiredService<IConfiguration>();
        
        // Verify account exists
        var accountsSection = config.GetSection("Accounts");
        var targetAccount = accountsSection.GetChildren()
            .FirstOrDefault(a => a["Alias"]?.Equals(targetAlias, StringComparison.OrdinalIgnoreCase) == true);

        if (targetAccount == null)
        {
            System.Console.WriteLine($"Error: Account '{targetAlias}' not found in configuration.");
            return;
        }

        var accountNo = targetAccount["Number"];
        if (string.IsNullOrEmpty(accountNo))
        {
            logger.LogError($"Account Number is missing for account '{targetAlias}'.");
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
