using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using yQuant.Core.Models;
using yQuant.Infra.Redis.Interfaces;

namespace yQuant.App.Console.Services;

/// <summary>
/// Redis-based repository for stock catalog data.
/// </summary>
public class StockCatalogRepository
{
    private readonly IRedisService _redisService;
    private readonly ILogger<StockCatalogRepository> _logger;
    private const string KeyPrefix = "stock:";
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromHours(25);

    public StockCatalogRepository(
        IRedisService redisService,
        ILogger<StockCatalogRepository> logger)
    {
        _redisService = redisService;
        _logger = logger;
    }

    public async Task SaveBatchAsync(IEnumerable<Stock> stocks, CancellationToken cancellationToken = default)
    {
        var db = _redisService.Connection.GetDatabase();
        var batch = db.CreateBatch();
        var tasks = new List<Task>();

        foreach (var stock in stocks)
        {
            var key = $"{KeyPrefix}{stock.Ticker}";
            tasks.Add(batch.HashSetAsync(key, new HashEntry[]
            {
                new("name", stock.Name),
                new("exchange", stock.Exchange),
                new("currency", stock.Currency.ToString())
            }));
            tasks.Add(batch.KeyExpireAsync(key, DefaultExpiration));
        }

        batch.Execute();
        await Task.WhenAll(tasks);

        _logger.LogDebug("Saved {Count} stocks to Redis", stocks.Count());
    }

    public async Task<Stock?> GetByTickerAsync(string ticker, CancellationToken cancellationToken = default)
    {
        var db = _redisService.Connection.GetDatabase();
        var key = $"{KeyPrefix}{ticker}";

        var entries = await db.HashGetAllAsync(key);
        if (entries.Length == 0)
        {
            return null;
        }

        var dict = entries.ToDictionary(
            e => e.Name.ToString(),
            e => e.Value.ToString()
        );

        return new Stock
        {
            Ticker = ticker,
            Name = dict.GetValueOrDefault("name", ""),
            Exchange = dict.GetValueOrDefault("exchange", ""),
            Currency = Enum.TryParse<CurrencyType>(dict.GetValueOrDefault("currency", "USD"), out var currency)
                ? currency
                : CurrencyType.USD
        };
    }
}
