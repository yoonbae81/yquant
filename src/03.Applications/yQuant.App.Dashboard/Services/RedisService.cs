using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Text.Json;
using yQuant.Core.Models;

namespace yQuant.App.Dashboard.Services;

public class RedisService : IHostedService, IDisposable
{
    private readonly ILogger<RedisService> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IConfiguration _configuration;

    private ConcurrentDictionary<string, Account> _accounts = new();
    private ConcurrentDictionary<string, ConcurrentDictionary<string, Position>> _positions = new(); // AccountId -> Ticker -> Position

    private Timer? _timer;
    private TimeSpan _syncInterval;

    public event Action? OnChange;

    public RedisService(ILogger<RedisService> logger, IConnectionMultiplexer redis, IConfiguration configuration)
    {
        _logger = logger;
        _redis = redis;
        _configuration = configuration;
        _syncInterval = TimeSpan.FromSeconds(configuration.GetValue<int>("RedisSyncIntervalSeconds", 1));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RedisService is starting.");
        _timer = new Timer(DoWork, null, TimeSpan.Zero, _syncInterval);
        return Task.CompletedTask;
    }

    private async void DoWork(object? state)
    {
        _logger.LogDebug("Fetching latest data from Redis...");
        try
        {
            var db = _redis.GetDatabase();

            // Fetch Accounts
            var accountKeys = await _redis.GetServer(_redis.GetEndPoints().First()).KeysAsync(pattern: "cache:account:*").ToListAsync();
            var fetchedAccounts = new ConcurrentDictionary<string, yQuant.Core.Models.Account>();
            foreach (var key in accountKeys)
            {
                var accountJson = await db.StringGetAsync(key);
                if (accountJson.HasValue)
                {
                    var account = JsonSerializer.Deserialize<yQuant.Core.Models.Account>(accountJson.ToString());
                    if (account != null)
                    {
                        fetchedAccounts[account.Alias ?? account.Id] = account;
                    }
                }
            }
            _accounts = fetchedAccounts; // Atomically replace the collection

            // Fetch Positions
            var positionKeys = await _redis.GetServer(_redis.GetEndPoints().First()).KeysAsync(pattern: "cache:position:*").ToListAsync();
            var fetchedPositions = new ConcurrentDictionary<string, ConcurrentDictionary<string, yQuant.Core.Models.Position>>();
            foreach (var key in positionKeys)
            {
                var positionJson = await db.StringGetAsync(key);
                if (positionJson.HasValue)
                {
                    var position = JsonSerializer.Deserialize<yQuant.Core.Models.Position>(positionJson.ToString());
                    if (position != null && !string.IsNullOrEmpty(position.AccountAlias) && !string.IsNullOrEmpty(position.Ticker))
                    {
                        fetchedPositions.GetOrAdd(position.AccountAlias, new ConcurrentDictionary<string, yQuant.Core.Models.Position>())[position.Ticker] = position;
                    }
                }
            }
            _positions = fetchedPositions; // Atomically replace the collection

            OnChange?.Invoke(); // Notify listeners that data has changed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching data from Redis.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RedisService is stopping.");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public virtual IEnumerable<Account> GetAccounts() => _accounts.Values;
    public IEnumerable<Position> GetPositions(string accountAlias) => _positions.TryGetValue(accountAlias, out var accountPositions) ? accountPositions.Values : Enumerable.Empty<Position>();
    public Position? GetPosition(string accountAlias, string ticker) => _positions.TryGetValue(accountAlias, out var accountPositions) && accountPositions.TryGetValue(ticker, out var position) ? position : null;

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
