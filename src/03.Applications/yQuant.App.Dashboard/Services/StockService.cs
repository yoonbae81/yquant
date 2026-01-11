using Microsoft.Extensions.Logging;
using yQuant.Infra.Valkey.Interfaces;
using yQuant.Infra.Valkey.Services;
using StackExchange.Redis;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.App.Dashboard.Services;

public class StockService
{
    private readonly ILogger<StockService> _logger;
    private readonly IValkeyService _messageValkey;
    private readonly IStockCatalogRepository _catalogRepository;
    private const string KeyPrefix = "stock:";

    public StockService(
        ILogger<StockService> logger,
        IValkeyService messageValkey,
        IStockCatalogRepository catalogRepository)
    {
        _logger = logger;
        _messageValkey = messageValkey;
        _catalogRepository = catalogRepository;
    }

    public async Task<string> GetStockNameAsync(string ticker)
    {
        var stock = await _catalogRepository.GetByTickerAsync(ticker);
        return stock?.Name ?? string.Empty;
    }

    public async Task<(string Name, yQuant.Core.Models.ExchangeCode Exchange, yQuant.Core.Models.CurrencyType Currency)?> GetStockInfoAsync(string ticker)
    {
        var stock = await _catalogRepository.GetByTickerAsync(ticker);
        if (stock == null) return null;

        if (!Enum.TryParse<yQuant.Core.Models.ExchangeCode>(stock.Exchange, true, out var exchange))
            exchange = yQuant.Core.Models.ExchangeCode.Unknown;

        return (stock.Name, exchange, stock.Currency);
    }

    public async Task<Dictionary<string, string>> GetStockNamesAsync(IEnumerable<string> tickers)
    {
        var stocks = await _catalogRepository.GetByTickersAsync(tickers);
        return stocks.ToDictionary(s => s.Ticker, s => s.Name, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<(decimal Price, yQuant.Core.Models.CurrencyType Currency)?> GetCurrentPriceAsync(string ticker)
    {
        if (string.IsNullOrWhiteSpace(ticker)) return null;

        try
        {
            var db = _messageValkey.Connection.GetDatabase();
            var key = $"{KeyPrefix}{ticker}";

            var priceValue = await db.HashGetAsync(key, "price");
            var currencyValue = await db.HashGetAsync(key, "currency");

            _logger.LogInformation("Valkey read for {Ticker}: price={Price}, currency={Currency}",
                ticker,
                priceValue.HasValue ? priceValue.ToString() : "null",
                currencyValue.HasValue ? currencyValue.ToString() : "null");

            // If we have both price and currency, return them
            if (priceValue.HasValue && currencyValue.HasValue)
            {
                if (decimal.TryParse(priceValue.ToString(), out var price) && price > 0)
                {
                    if (Enum.TryParse<yQuant.Core.Models.CurrencyType>(currencyValue.ToString(), true, out var currency))
                    {
                        _logger.LogInformation("Returning price for {Ticker}: {Price} {Currency}", ticker, price, currency);
                        return (price, currency);
                    }
                }
            }

            // If we have currency but no price, request price from BrokerGateway
            if (currencyValue.HasValue)
            {
                _logger.LogInformation("Price not available for {Ticker}, requesting from BrokerGateway", ticker);
                await RequestPriceFromBroker(ticker);

                // Wait a bit and retry
                await Task.Delay(500);
                priceValue = await db.HashGetAsync(key, "price");

                if (priceValue.HasValue && decimal.TryParse(priceValue.ToString(), out var price) && price > 0)
                {
                    if (Enum.TryParse<yQuant.Core.Models.CurrencyType>(currencyValue.ToString(), true, out var currency))
                    {
                        _logger.LogInformation("Price fetched for {Ticker}: {Price} {Currency}", ticker, price, currency);
                        return (price, currency);
                    }
                }

                // Return null price but valid currency so order can still be placed
                if (Enum.TryParse<yQuant.Core.Models.CurrencyType>(currencyValue.ToString(), true, out var curr))
                {
                    _logger.LogWarning("Price still not available for {Ticker}, returning currency only", ticker);
                    return (0, curr);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching current price for {Ticker}", ticker);
            return null;
        }
    }

    private async Task RequestPriceFromBroker(string ticker)
    {
        try
        {
            var query = new yQuant.Core.Models.Query
            {
                QueryType = "price",
                Target = ticker
            };

            var subscriber = _messageValkey.Connection.GetSubscriber();
            var message = System.Text.Json.JsonSerializer.Serialize(query);
            await subscriber.PublishAsync(RedisChannel.Literal("query"), message);

            _logger.LogInformation("Published price query for {Ticker}", ticker);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing price query for {Ticker}", ticker);
        }
    }
}
