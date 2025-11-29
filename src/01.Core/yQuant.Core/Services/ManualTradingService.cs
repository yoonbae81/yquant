using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Input;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Core.Services;

public class ManualTradingService : IManualTradingUseCase
{
    private readonly IBrokerAdapter _brokerAdapter;
    private readonly ILogger<ManualTradingService> _logger;

    public ManualTradingService(IBrokerAdapter brokerAdapter, ILogger<ManualTradingService> logger)
    {
        _brokerAdapter = brokerAdapter;
        _logger = logger;
    }

    public async Task PlaceManualOrderAsync(Order order)
    {
        _logger.LogInformation("Placing manual order: {Action} {Ticker} {Qty} @ {Price}", 
            order.Action, order.Ticker, order.Qty, order.Price);

        // Basic validation could go here

        var result = await _brokerAdapter.PlaceOrderAsync(order, order.AccountAlias);
        _logger.LogInformation("Manual order result: {IsSuccess} - {Message}", result.IsSuccess, result.Message);
    }
}
