using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Input;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Core.Ports.Output.Policies;

namespace yQuant.Core.Services;

public class OrderCompositionService : IOrderCompositionUseCase
{
    private readonly IEnumerable<IMarketRule> _marketRules;
    private readonly IPositionSizer _positionSizer;
    private readonly IAccountRepository _accountRepository;
    private readonly IAccountRegistry _accountRegistry;
    private readonly IOrderPublisher _orderPublisher;
    private readonly ILogger<OrderCompositionService> _logger;

    public OrderCompositionService(
        IEnumerable<IMarketRule> marketRules,
        IPositionSizer positionSizer,
        IAccountRepository accountRepository,
        IAccountRegistry accountRegistry,
        IOrderPublisher orderPublisher,
        ILogger<OrderCompositionService> logger)
    {
        _marketRules = marketRules;
        _positionSizer = positionSizer;
        _accountRepository = accountRepository;
        _accountRegistry = accountRegistry;
        _orderPublisher = orderPublisher;
        _logger = logger;
    }

    public async Task ProcessSignalAsync(Signal signal)
    {
        _logger.LogInformation("Processing signal for {Ticker} on {Exchange}", signal.Ticker, signal.Exchange);

        // 1. Select Market Rule
        var marketRule = _marketRules.FirstOrDefault(r => r.CanHandle(signal.Exchange));
        if (marketRule == null)
        {
            _logger.LogWarning("No market rule found for exchange: {Exchange}", signal.Exchange);
            return;
        }

        // 2. Check Market Open
        if (!marketRule.IsMarketOpen(signal.Timestamp))
        {
            _logger.LogWarning("Market {Exchange} is closed. Signal ignored.", signal.Exchange);
            return;
        }

        // 3. Account Selection
        var targetCurrency = marketRule.GetCurrency();
        var accountAlias = _accountRegistry.GetAccountAliasForCurrency(targetCurrency);

        if (string.IsNullOrEmpty(accountAlias))
        {
            _logger.LogError("No account mapping found for currency {Currency} for signal {SignalId}", targetCurrency, signal.Id);
            return;
        }

        // 4. Get Account State
        var account = await _accountRepository.GetAccountAsync(accountAlias);
        if (account == null)
        {
            _logger.LogWarning("Account {AccountAlias} not found. Cannot process signal.", accountAlias);
            return;
        }

        // 5. Position Sizing
        var order = _positionSizer.CalculatePositionSize(signal, account);
        if (order == null)
        {
            _logger.LogInformation("Position sizer returned null order for signal {SignalId}. Likely due to insufficient funds or policy rules.", signal.Id);
            return;
        }

        order.Exchange = signal.Exchange;
        order.Currency = signal.Currency ?? CurrencyType.USD;

        // 6. Validate Order
        if (!_positionSizer.ValidateOrder(order, account, out var failureReason))
        {
            _logger.LogWarning("Order validation failed for signal {SignalId}: {Reason}", signal.Id, failureReason);
            return;
        }

        // 7. Publish Order
        await _orderPublisher.PublishOrderAsync(order);
        _logger.LogInformation("Published order {OrderId} for signal {SignalId}.", order.Id, signal.Id);
    }
}
