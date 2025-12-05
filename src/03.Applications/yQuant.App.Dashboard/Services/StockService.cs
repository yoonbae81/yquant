using Microsoft.Extensions.Logging;
using yQuant.Infra.Redis.Interfaces;
using StackExchange.Redis;

namespace yQuant.App.Dashboard.Services;

public class StockService
{
    private readonly ILogger<StockService> _logger;
    private readonly IRedisService _redisService;
    private const string KeyPrefix = "stock:";

    public StockService(
        ILogger<StockService> logger,
        IRedisService redisService)
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

    public async Task<Dictionary<string, string>> GetStockNamesAsync(IEnumerable<string> tickers)
    {
        var result = new Dictionary<string, string>();
        var db = _redisService.Connection.GetDatabase();
        var batch = db.CreateBatch();
        var tasks = new List<Task<RedisValue>>();
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
}
