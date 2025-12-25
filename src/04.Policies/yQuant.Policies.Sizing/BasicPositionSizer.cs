using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Policies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace yQuant.Policies.Sizing;

public class BasicPositionSizer : IPositionSizer
{
    private readonly ILogger<BasicPositionSizer> _logger;
    private readonly PositionSizerSettings _settings;

    public BasicPositionSizer(ILogger<BasicPositionSizer> logger, IConfiguration configuration)
    {
        _logger = logger;
        _settings = new PositionSizerSettings();
        configuration.Bind(_settings);
    }

    public Order? CalculatePositionSize(Signal signal, Account account)
    {
        // 1. 기초 데이터 추출
        // In a real scenario, GetLastPrice would fetch the latest price.
        // For now, we'll use signal.Price if available, otherwise assume a price of 1 for calculation or return null.
        decimal currentPrice = signal.Price ?? 1m; // Placeholder: In real-world, fetch from market data

        if (currentPrice <= 0)
        {
            _logger.LogWarning("Invalid current price for signal {SignalId}. Cannot calculate position size.", signal.Id);
            return null;
        }

        // Default to KRW if signal currency is null, or use the account's primary currency if available
        CurrencyType accountCurrency = signal.Currency ?? CurrencyType.KRW; // Consider a more robust default or account's base currency

        decimal equity = account.GetTotalEquity(accountCurrency);
        decimal cash = account.Deposits.GetValueOrDefault(accountCurrency, 0);

        // 2. 설정된 파라미터 로드 (already loaded in _settings)

        // 3. 타겟 금액 산출
        decimal strengthFactor = Math.Clamp((decimal)(signal.Strength ?? 100) / 100m, 0m, 1m); // Default strength to 100 if null

        // Documentation formula: min(MaxAllocByRisk, MaxAllocByPort) * Normalize(Strength)
        // Let's implement MaxAllocByRisk and MaxAllocByPort first

        // Risk-Based Allocation
        decimal riskAmount = equity * _settings.MaxPositionRiskPct;
        decimal maxAllocByRisk = riskAmount / _settings.StopLossPct;

        // Portfolio Limitation
        decimal maxAllocByPort = equity * _settings.MaxPortfolioAllocPct;

        decimal targetAmount = Math.Min(maxAllocByRisk, maxAllocByPort) * strengthFactor;

        // 4. 예수금 제약 확인
        decimal actualAmount = Math.Min(targetAmount, cash);

        // 5. 수량 계산
        int qty = (int)Math.Floor(actualAmount / currentPrice);

        // 6. 최소 주문 금액 필터링
        if (qty * currentPrice < _settings.MinOrderAmt || qty <= 0)
        {
            _logger.LogInformation("Calculated order amount {CalculatedAmount} below MinOrderAmt {MinOrderAmt} or quantity is zero for signal {SignalId}.", qty * currentPrice, _settings.MinOrderAmt, signal.Id);
            return null; // 주문 미생성
        }

        // 7. Order 객체 반환
        return new Order
        {
            AccountAlias = account.Alias,
            Ticker = signal.Ticker,
            Action = signal.Action,
            Type = OrderType.Market, // Documentation specifies Market for composed orders
            Qty = qty,
            Price = currentPrice, // Assuming currentPrice is the price at which order is placed
            Timestamp = DateTime.UtcNow
        };
    }

    public bool ValidateOrder(Order order, Account account, out string failureReason)
    {
        // Simple validation: Ensure quantity is positive
        if (order.Qty <= 0)
        {
            failureReason = "Order quantity must be positive.";
            return false;
        }

        // Add more complex validation as needed, e.g., checking against account limits,
        // preventing duplicate orders for the same signal, etc.

        failureReason = string.Empty;
        return true;
    }

    private class PositionSizerSettings
    {
        public decimal MaxPositionRiskPct { get; set; }
        public decimal MaxPortfolioAllocPct { get; set; }
        public decimal StopLossPct { get; set; }
        public decimal MinOrderAmt { get; set; }
    }
}
