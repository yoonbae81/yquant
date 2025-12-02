using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using yQuant.App.OrderComposer;
using yQuant.App.OrderComposer.Adapters;
using yQuant.Core.Extensions;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Core.Ports.Output.Policies;
using yQuant.Infra.Redis.Extensions;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("sharedsettings.json", optional: false, reloadOnChange: true)
                     .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                     .AddJsonFile($"sharedsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                     .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

builder.Services.AddRedisMiddleware(builder.Configuration);

// Register Core Services
builder.Services.AddyQuantCore();

// Register Infrastructure Adapters
builder.Services.AddSingleton<IAccountRepository, RedisAccountRepository>();
builder.Services.AddSingleton<IAccountRegistry, ConfigAccountRegistry>();
builder.Services.AddSingleton<IOrderPublisher, RedisOrderPublisher>();

// Register Policies (Dynamic Loading)
builder.Services.AddSingleton<IPositionSizer>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<Program>>();
    var sizingConfig = config.GetSection("Policies:Sizing");

    var path = sizingConfig["Path"];
    var className = sizingConfig["Class"];

    if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(className))
    {
        throw new InvalidOperationException("Position Sizer policy is not configured correctly.");
    }

    try
    {
        var assembly = Assembly.LoadFrom(path);
        var type = assembly.GetType(className);
        if (type == null) throw new TypeLoadException($"Class {className} not found in {path}");

        var settings = sizingConfig.GetSection("Settings");
        // Create instance using DI to inject dependencies if needed, plus settings
        // Note: This assumes the policy constructor takes IConfigurationSection or bound settings object.
        // For simplicity, we'll assume ActivatorUtilities can handle it if we pass the settings section or object.
        // However, ActivatorUtilities.CreateInstance with extra args appends them.
        // If the policy needs specific settings object, we might need to bind it first.
        // Let's assume for now we pass IConfigurationSection 'Settings'.
        return (IPositionSizer)ActivatorUtilities.CreateInstance(sp, type, settings);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to load Position Sizer policy.");
        throw;
    }
});

builder.Services.AddSingleton<IEnumerable<IMarketRule>>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<Program>>();
    var marketsConfig = config.GetSection("Policies:Markets").GetChildren();
    var rules = new List<IMarketRule>();

    foreach (var marketConfig in marketsConfig)
    {
        var path = marketConfig["Path"];
        var className = marketConfig["Class"];

        if (!string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(className))
        {
            try
            {
                var assembly = Assembly.LoadFrom(path);
                var type = assembly.GetType(className);
                if (type != null)
                {
                    var settings = marketConfig.GetSection("Settings");
                    var rule = (IMarketRule)ActivatorUtilities.CreateInstance(sp, type, settings);
                    rules.Add(rule);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load Market Rule policy: {Path}", path);
            }
        }
    }

    return rules;
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

// Helper class for logger resolution in Program (not strictly needed if we use sp inside factory)
public partial class Program { }
