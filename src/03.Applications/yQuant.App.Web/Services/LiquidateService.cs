using Microsoft.Extensions.Logging;
using yQuant.Core.Models;

namespace yQuant.App.Web.Services;

public class LiquidateService
{
    private readonly OrderPublisher _orderPublisher;
    private readonly ILogger<LiquidateService> _logger;

    public LiquidateService(
        OrderPublisher orderPublisher,
        ILogger<LiquidateService> logger)
    {
        _orderPublisher = orderPublisher;
        _logger = logger;
    }

    /// <summary>
    /// Filter positions by country and BuyReason
    /// </summary>
    public List<Position> FilterPositions(
        List<Position> positions,
        string? countryFilter,
        string? buyReasonFilter)
    {
        var filtered = positions.AsEnumerable();

        // Apply country filter
        if (!string.IsNullOrEmpty(countryFilter) && countryFilter != "All")
        {
            var currencyType = countryFilter switch
            {
                "KR" => CurrencyType.KRW,
                "US" => CurrencyType.USD,
                _ => (CurrencyType?)null
            };

            if (currencyType.HasValue)
            {
                filtered = filtered.Where(p => p.Currency == currencyType.Value);
            }
        }

        // Apply BuyReason filter
        if (!string.IsNullOrEmpty(buyReasonFilter) && buyReasonFilter != "All")
        {
            if (buyReasonFilter == "Unknown")
            {
                filtered = filtered.Where(p => string.IsNullOrEmpty(p.BuyReason));
            }
            else
            {
                filtered = filtered.Where(p => p.BuyReason == buyReasonFilter);
            }
        }

        return filtered.ToList();
    }

    /// <summary>
    /// Extract unique BuyReason values for dropdown (includes "Unknown" if needed)
    /// </summary>
    public List<string> GetBuyReasonOptions(List<Position> positions)
    {
        var options = new List<string> { "All" };
        var buyReasons = new HashSet<string>();

        bool hasUnknown = false;

        foreach (var position in positions)
        {
            if (string.IsNullOrEmpty(position.BuyReason))
            {
                hasUnknown = true;
            }
            else
            {
                buyReasons.Add(position.BuyReason);
            }
        }

        // Add standard options if they exist
        if (buyReasons.Contains("Manual"))
        {
            options.Add("Manual");
            buyReasons.Remove("Manual");
        }

        if (buyReasons.Contains("Schedule"))
        {
            options.Add("Schedule");
            buyReasons.Remove("Schedule");
        }

        // Add remaining strategy names (sorted)
        options.AddRange(buyReasons.OrderBy(r => r));

        // Add Unknown if needed
        if (hasUnknown)
        {
            options.Add("Unknown");
        }

        return options;
    }

    /// <summary>
    /// Create and publish bulk market sell orders for the given positions
    /// </summary>
    public async Task<int> LiquidatePositionsAsync(
        List<Position> positions,
        string accountAlias)
    {
        if (!positions.Any())
        {
            _logger.LogWarning("LiquidatePositionsAsync called with no positions for account {AccountAlias}", accountAlias);
            return 0;
        }

        _logger.LogInformation("Liquidating {Count} positions for account {AccountAlias}", positions.Count, accountAlias);

        int successCount = 0;

        foreach (var position in positions)
        {
            try
            {
                var order = new Order
                {
                    AccountAlias = accountAlias,
                    Ticker = position.Ticker,
                    Exchange = position.Exchange ?? ExchangeCode.KOSPI,
                    Currency = position.Currency,
                    Action = OrderAction.Sell,
                    Type = OrderType.Market,
                    Qty = position.Qty,
                    Price = 0, // Market order
                    BuyReason = position.BuyReason // Preserve original BuyReason for tracking
                };

                await _orderPublisher.PublishOrderAsync(order);
                successCount++;

                _logger.LogInformation("Liquidation order published: {Ticker} x {Qty} from {AccountAlias}",
                    position.Ticker, position.Qty, accountAlias);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish liquidation order for {Ticker} in {AccountAlias}",
                    position.Ticker, accountAlias);
            }
        }

        _logger.LogInformation("Liquidation complete: {SuccessCount}/{TotalCount} orders published",
            successCount, positions.Count);

        return successCount;
    }
}
