using Microsoft.Extensions.Logging;
using yQuant.Infra.Valkey.Interfaces;
using StackExchange.Redis;

namespace yQuant.App.Dashboard.Services;

public class StockService
{
    private readonly ILogger<StockService> _logger;
    private readonly IValkeyService _redisService;
    private const string KeyPrefix = "stock:";

    public StockService(
        ILogger<StockService> logger,
        IValkeyService redisService)
    {
        _logger = logger;
        _redisService = redisService;
    }

    public async Task<string> GetStockNameAsync(string ticker)
    {
        if (string.IsNullOrWhiteSpace(ticker))
            return string.Empty;

        try
        {
            var db = _redisService.Connection.GetDatabase();
            var key = $"{KeyPrefix}{ticker}";
            var name = await db.HashGetAsync(key, "name");

            return name.HasValue ? name.ToString() : string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching stock name for {Ticker}", ticker);
            return string.Empty;
        }
    }

    public async Task<(string Name, yQuant.Core.Models.ExchangeCode Exchange, yQuant.Core.Models.CurrencyType Currency)?> GetStockInfoAsync(string ticker)
    {
        if (string.IsNullOrWhiteSpace(ticker)) return null;

        try
        {
            var db = _redisService.Connection.GetDatabase();
            var key = $"{KeyPrefix}{ticker}";
            var entries = await db.HashGetAllAsync(key);

            if (entries.Length == 0) return null;

            var dict = entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());

            var name = dict.GetValueOrDefault("name", "");

            if (!Enum.TryParse<yQuant.Core.Models.ExchangeCode>(dict.GetValueOrDefault("exchange"), true, out var exchange))
                exchange = yQuant.Core.Models.ExchangeCode.Unknown;

            if (!Enum.TryParse<yQuant.Core.Models.CurrencyType>(dict.GetValueOrDefault("currency"), true, out var currency))
                currency = yQuant.Core.Models.CurrencyType.USD;

            return (name, exchange, currency);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching stock info for {Ticker}", ticker);
            return null;
        }
    }

    public async Task<Dictionary<string, string>> GetStockNamesAsync(IEnumerable<string> tickers)
    {
        var result = new Dictionary<string, string>();
        var db = _redisService.Connection.GetDatabase();
        var batch = db.CreateBatch();
        var tasks = new List<Task<ValkeyValue>>();
        var tickerList = tickers.Distinct().ToList();

        foreach (var ticker in tickerList)
        {
            var key = $"{KeyPrefix}{ticker}";
            tasks.Add(batch.HashGetAsync(key, "name"));
        }

        batch.Execute();
        await Task.WhenAll(tasks);

        for (int i = 0; i < tickerList.Count; i++)
        {
            var name = tasks[i].Result;
            if (name.HasValue)
            {
                result[tickerList[i]] = name.ToString();
            }
        }

        return result;
    }

    public async Task<(decimal Price, yQuant.Core.Models.CurrencyType Currency)?> GetCurrentPriceAsync(string ticker)
    {
        if (string.IsNullOrWhiteSpace(ticker)) return null;

        try
        {
            var db = _redisService.Connection.GetDatabase();
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

            var subscriber = _redisService.Connection.GetSubscriber();
            var message = System.Text.Json.JsonSerializer.Serialize(query);
            await subscriber.PublishAsync(ValkeyChannel.Literal("query"), message);

            _logger.LogInformation("Published price query for {Ticker}", ticker);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing price query for {Ticker}", ticker);
        }
    }
}
