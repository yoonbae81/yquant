using Microsoft.Extensions.DependencyInjection;
using yQuant.Core.Ports.Input;
using yQuant.Core.Services;

namespace yQuant.Core.Extensions;

public static class CoreServiceExtensions
{
    public static IServiceCollection AddyQuantCore(this IServiceCollection services)
    {
        // Register Use Cases / Services
        services.AddScoped<IOrderCompositionUseCase, OrderCompositionService>();
        services.AddScoped<IManualTradingUseCase, ManualTradingService>();

        return services;
    }
}
