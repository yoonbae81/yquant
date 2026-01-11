using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Persistence;

public static class PersistenceExtensions
{
    public static IServiceCollection AddMariaDbPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        // Register MariaDB DbContext
        var connectionString = configuration.GetConnectionString("MariaDB")
            ?? throw new InvalidOperationException("MariaDB connection string is missing.");

        services.AddDbContext<MariaDbContext>(options =>
        {
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mySqlOptions =>
            {
                mySqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
            });
        }, ServiceLifetime.Scoped);

        // Register the MariaDB repositories as Singleton
        // Note: Repositories will create their own DbContext scopes internally
        services.TryAddScoped<ITradeRepository, MariaDbTradeRepository>();
        services.TryAddSingleton<IStockCatalogRepository, MariaDbStockCatalogRepository>();
        services.TryAddScoped<IKisTokenRepository, MariaDbKisTokenRepository>();
        services.TryAddSingleton<IScheduledOrderRepository, MariaDbScheduledOrderRepository>();
        services.TryAddScoped<IDailySnapshotRepository, MariaDbDailySnapshotRepository>();

        return services;
    }

    public static IServiceCollection AddTradeArchiver(this IServiceCollection services)
    {
        services.AddHostedService<TradeArchiverService>();
        return services;
    }

    public static async Task InitializeMariaDbPersistenceAsync(this IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<MariaDbContext>>();
        try
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MariaDbContext>();

            logger.LogInformation("Ensuring MariaDB database is created and migrated...");
            await context.Database.EnsureCreatedAsync();

            logger.LogInformation("MariaDB persistence initialized successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize MariaDB persistence schema.");
            throw;
        }
    }
}
