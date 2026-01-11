using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Persistence;

public static class PersistenceExtensions
{
    public static IServiceCollection AddFirebirdPersistence(this IServiceCollection services)
    {
        // Register the Firebird repositories
        services.TryAddSingleton<FirebirdTradeRepository>();
        services.TryAddSingleton<ITradeRepository>(sp => sp.GetRequiredService<FirebirdTradeRepository>());

        services.TryAddSingleton<FirebirdStockCatalogRepository>();
        services.TryAddSingleton<IStockCatalogRepository>(sp => sp.GetRequiredService<FirebirdStockCatalogRepository>());

        services.TryAddSingleton<FirebirdKisTokenRepository>();
        services.TryAddSingleton<IKisTokenRepository>(sp => sp.GetRequiredService<FirebirdKisTokenRepository>());

        services.TryAddSingleton<FirebirdScheduledOrderRepository>();
        services.TryAddSingleton<IScheduledOrderRepository>(sp => sp.GetRequiredService<FirebirdScheduledOrderRepository>());

        services.TryAddSingleton<FirebirdDailySnapshotRepository>();
        services.TryAddSingleton<IDailySnapshotRepository>(sp => sp.GetRequiredService<FirebirdDailySnapshotRepository>());

        return services;
    }

    public static IServiceCollection AddTradeArchiver(this IServiceCollection services)
    {
        services.AddHostedService<TradeArchiverService>();
        return services;
    }

    public static async Task InitializeFirebirdPersistenceAsync(this IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<FirebirdTradeRepository>>();
        try
        {
            var tradeRepo = services.GetRequiredService<FirebirdTradeRepository>();
            await tradeRepo.InitializeAsync();

            var catalogRepo = services.GetRequiredService<FirebirdStockCatalogRepository>();
            await catalogRepo.InitializeAsync();

            var tokenRepo = services.GetRequiredService<FirebirdKisTokenRepository>();
            await tokenRepo.InitializeAsync();

            var scheduledRepo = services.GetRequiredService<FirebirdScheduledOrderRepository>();
            await scheduledRepo.InitializeAsync();

            var snapshotRepo = services.GetRequiredService<FirebirdDailySnapshotRepository>();
            await snapshotRepo.InitializeAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize Firebird persistence schema.");
            throw;
        }
    }
}
