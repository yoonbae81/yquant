using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
            System.Console.WriteLine("  assets");
            System.Console.WriteLine("  price <ticker>");
            System.Console.WriteLine("  buy <ticker> <qty> [price]");
            System.Console.WriteLine("  sell <ticker> <qty> [price]");
            System.Console.WriteLine("  report export");
            return;
        }

        var targetAlias = args[0];
        var cmd = args[1].ToLower();

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
                    // Will be handled in Main after build, but we can't easily stop here without throwing
                    // Registering a dummy or throwing exception? 
                    // Better to let it fail gracefully in Main, but we need services.
                    // Let's register nulls or throw meaningful exception that we catch?
                    // Actually, if we throw here, Host.Build() throws.
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

                services.AddHttpClient<IKisConnector, KisConnector>(client =>
                {
                    client.BaseAddress = new Uri(baseUrl);
                });

                // Register KisConnector manually to inject config values
                services.AddSingleton<IKisConnector>(sp =>
                {
                    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(KisConnector));
                    var logger = sp.GetRequiredService<ILogger<KisConnector>>();
                    var redis = sp.GetService<IRedisService>();
                    
                    var apiConfig = KISApiConfig.Load(Path.Combine(AppContext.BaseDirectory, "API"));
                    apiConfig.BaseUrl = baseUrl;
                    return new KisConnector(httpClient, logger, redis, targetAlias, appKey!, appSecret!, apiConfig);
                });

                services.AddSingleton<KisBrokerAdapter>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<KisBrokerAdapter>>();
                    var client = sp.GetRequiredService<IKisConnector>();
                    var prefix = accountNo?.Length >= 8 ? accountNo.Substring(0, 8) : "00000000";
                    return new KisBrokerAdapter(logger, client, prefix, targetAlias);
                });

                services.AddSingleton<IPerformanceRepository, JsonPerformanceRepository>();
                services.AddSingleton<IQuantStatsService, QuantStatsService>();
                services.AddSingleton<yQuant.Core.Services.AssetService>();
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
        var adapter = host.Services.GetService<KisBrokerAdapter>();
        if (adapter == null)
        {
            System.Console.WriteLine("Error: Failed to initialize services. Check credentials.");
            return;
        }

        var assetService = host.Services.GetRequiredService<yQuant.Core.Services.AssetService>();

        try
        {
            if (cmd == "assets")
            {
                var account = await assetService.GetAccountOverviewAsync(accountNo);
                
                System.Console.WriteLine("========================================");
                System.Console.WriteLine($"Account Summary: {account.Alias} ({account.Number})");
                System.Console.WriteLine("========================================");
                
                System.Console.WriteLine("\n[Deposits]");
                foreach (var deposit in account.Deposits)
                {
                    System.Console.WriteLine($"- {deposit.Key}: {deposit.Value:N2}");
                }

                System.Console.WriteLine($"\nTotal Equity (KRW): {account.GetTotalEquity(CurrencyType.KRW):N0} KRW");
                System.Console.WriteLine($"Total Equity (USD): {account.GetTotalEquity(CurrencyType.USD):N2} USD");

                System.Console.WriteLine("\n[Positions]");
                System.Console.WriteLine($"{"Ticker",-10} {"Qty",-10} {"AvgPrice",-15} {"CurPrice",-15} {"PnL",-15} {"Return%",-10}");
                System.Console.WriteLine(new string('-', 80));

                foreach (var pos in account.Positions)
                {
                    var pnl = pos.UnrealizedPnL;
                    var returnRate = pos.AvgPrice != 0 ? (pos.CurrentPrice - pos.AvgPrice) / pos.AvgPrice * 100 : 0;
                    
                    System.Console.WriteLine($"{pos.Ticker,-10} {pos.Qty,-10} {pos.AvgPrice,-15:N2} {pos.CurrentPrice,-15:N2} {pnl,-15:N2} {returnRate,-10:F2}%");
                }
                System.Console.WriteLine("========================================");
            }
            else if (cmd == "price" && args.Length > 2)
            {
                var ticker = args[2];
                var priceInfo = await adapter.GetPriceAsync(ticker);
                System.Console.WriteLine($"Ticker: {ticker}");
                System.Console.WriteLine($"Price: {priceInfo.CurrentPrice:N2}");
                System.Console.WriteLine($"Change: {priceInfo.ChangeRate:N2}%");
            }
            else if ((cmd == "buy" || cmd == "sell") && args.Length >= 4)
            {
                var ticker = args[2];
                if (!decimal.TryParse(args[3], out var qty))
                {
                    System.Console.WriteLine("Invalid quantity.");
                    return;
                }

                decimal? price = null;
                var orderType = OrderType.Market;

                if (args.Length > 4)
                {
                    if (!decimal.TryParse(args[4], out var p))
                    {
                        System.Console.WriteLine("Invalid price.");
                        return;
                    }
                    price = p;
                    orderType = OrderType.Limit;
                }

                var order = new Order
                {
                    AccountAlias = targetAlias,
                    Ticker = ticker,
                    Action = cmd == "buy" ? OrderAction.Buy : OrderAction.Sell,
                    Type = orderType,
                    Qty = qty,
                    Price = price,
                    Timestamp = DateTime.UtcNow
                };

                logger.LogInformation("Placing {Type} {Action} order for {Ticker}: {Qty} @ {Price}", order.Type, order.Action, ticker, qty, price?.ToString() ?? "Market");
                var result = await adapter.PlaceOrderAsync(order, accountNo);
                System.Console.WriteLine($"Order Result: {(result.IsSuccess ? "Success" : "Failed")}");
                System.Console.WriteLine($"Message: {result.Message}");
                if (!string.IsNullOrEmpty(result.BrokerOrderId))
                {
                    System.Console.WriteLine($"Broker Order ID: {result.BrokerOrderId}");
                }
            }
            else if (cmd == "report")
            {
                // Usage: yquant <alias> report export
                if (args.Length > 2 && args[2].ToLower() == "export")
                {
                    var repo = host.Services.GetRequiredService<IPerformanceRepository>();
                    var service = host.Services.GetRequiredService<IQuantStatsService>();

                    var logs = await repo.GetLogsAsync(targetAlias);
                    if (!logs.Any())
                    {
                        System.Console.WriteLine($"No performance logs found for account: {targetAlias}");
                        return;
                    }

                    var csv = service.GenerateCsvReport(logs);
                    var filename = $"report_{targetAlias}_{DateTime.Now:yyyyMMdd}.csv";
                    await File.WriteAllTextAsync(filename, csv);
                    System.Console.WriteLine($"Exported to {filename}");
                }
                else
                {
                    System.Console.WriteLine($"Usage: yquant {targetAlias} report export");
                }
            }
            else
            {
                System.Console.WriteLine("Invalid command or arguments.");
                System.Console.WriteLine($"Usage: yquant {targetAlias} <command> [args...]");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during execution.");
        }
    }
}
