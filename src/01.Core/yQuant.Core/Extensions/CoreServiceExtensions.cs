using Microsoft.Extensions.DependencyInjection;
using yQuant.Core.Ports.Input;
using yQuant.Core.Services;

namespace yQuant.Core.Extensions;

public static class CoreServiceExtensions
{
    public static IServiceCollection AddyQuantCore(this IServiceCollection services, bool includeManualTrading = true)
    {
        // Register Use Cases / Services
        services.AddScoped<IOrderCompositionUseCase, OrderCompositionService>();

        // Only register ManualTradingService if requested (requires IBrokerAdapter)
        if (includeManualTrading)
        {
            services.AddScoped<IManualTradingUseCase, ManualTradingService>();
        }

        return services;
    }
}
