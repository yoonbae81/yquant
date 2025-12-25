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
    private readonly IEnumerable<IPositionSizer> _positionSizers;
    private readonly IStrategyPolicyMapper _policyMapper;
    private readonly IAccountRepository _accountRepository;
    private readonly IStrategyAccountMapper _accountMapper;
    private readonly IOrderPublisher _orderPublisher;
    private readonly ILogger<OrderCompositionService> _logger;

    public OrderCompositionService(
        IEnumerable<IMarketRule> marketRules,
        IEnumerable<IPositionSizer> positionSizers,
        IStrategyPolicyMapper policyMapper,
        IAccountRepository accountRepository,
        IStrategyAccountMapper accountMapper,
        IOrderPublisher orderPublisher,
        ILogger<OrderCompositionService> logger)
    {
        _marketRules = marketRules;
        _positionSizers = positionSizers;
        _policyMapper = policyMapper;
        _accountRepository = accountRepository;
        _accountMapper = accountMapper;
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
        var accountAliases = _accountMapper.GetAccountAliasesForStrategy(signal.Strategy);

        if (!accountAliases.Any())
        {
            _logger.LogError("No account mapping found for strategy {Strategy} for signal {SignalId}", signal.Strategy, signal.Id);
            return;
        }

        foreach (var accountAlias in accountAliases)
        {
            try
            {
                // 4. Get Account State
                var account = await _accountRepository.GetAccountAsync(accountAlias);
                if (account == null)
                {
                    _logger.LogWarning("Account {AccountAlias} not found. Skipping for strategy {Strategy}.", accountAlias, signal.Strategy);
                    continue;
                }

                // 5. Select Sizing Policy and Calculate
                var policyName = _policyMapper.GetSizingPolicyName(signal.Strategy);
                var positionSizer = _positionSizers.FirstOrDefault(s =>
                    s.GetType().Name.StartsWith(policyName, StringComparison.OrdinalIgnoreCase));

                if (positionSizer == null)
                {
                    _logger.LogWarning("No position sizer found for policy {PolicyName} (Strategy: {Strategy}). Skipping account {AccountAlias}.", policyName, signal.Strategy, accountAlias);
                    continue;
                }

                var order = positionSizer.CalculatePositionSize(signal, account);
                if (order == null)
                {
                    _logger.LogInformation("Position sizer {PolicyName} returned null order for signal {SignalId} and account {AccountAlias}.", policyName, signal.Id, accountAlias);
                    continue;
                }

                if (Enum.TryParse<ExchangeCode>(signal.Exchange, true, out var exchangeCode))
                {
                    order.Exchange = exchangeCode;
                }
                else
                {
                    _logger.LogWarning("Invalid Exchange Code in Signal: {Exchange}", signal.Exchange);
                    order.Exchange = ExchangeCode.NASDAQ;
                }

                order.Currency = signal.Currency ?? marketRule.GetCurrency();
                order.BuyReason = $"Webhook:{signal.Strategy}";

                // 6. Validate Order
                if (!positionSizer.ValidateOrder(order, account, out var failureReason))
                {
                    _logger.LogWarning("Order validation failed for signal {SignalId} on account {AccountAlias}: {Reason}", signal.Id, accountAlias, failureReason);
                    continue;
                }

                // 7. Publish Order
                await _orderPublisher.PublishOrderAsync(order);
                _logger.LogInformation("Published order {OrderId} for signal {SignalId} on account {AccountAlias}.", order.Id, signal.Id, accountAlias);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process signal {SignalId} for account {AccountAlias}", signal.Id, accountAlias);
            }
        }
    }
}
