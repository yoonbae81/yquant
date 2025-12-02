using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;
using yQuant.Infra.Redis.Interfaces;

namespace yQuant.App.Dashboard.Services;

public class RedisService : IHostedService, IDisposable
{
    private readonly ILogger<RedisService> _logger;
    private readonly IRedisService _redisService;
    private readonly IBrokerAdapterFactory _adapterFactory;

    public RedisService(
        ILogger<RedisService> logger,
        IRedisService redisService,
        IBrokerAdapterFactory adapterFactory)
    {
        _logger = logger;
        _redisService = redisService;
        _adapterFactory = adapterFactory;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dashboard RedisService started.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dashboard RedisService stopped.");
        return Task.CompletedTask;
    }

    public event Action? OnChange;

    public void Refresh() => OnChange?.Invoke();

    public IEnumerable<Account> GetAccounts()
    {
        var aliases = _adapterFactory.GetAvailableAccounts();
        var accounts = new List<Account>();
        foreach (var alias in aliases)
        {
            var adapter = _adapterFactory.GetAdapter(alias);
            if (adapter != null)
            {
                // Assuming adapter.Account returns the Account object with at least the Alias populated
                // If RedisBrokerClient doesn't implement Account property fully, we might need to check that.
                accounts.Add(adapter.Account);
            }
        }
        return accounts;
    }

    public IEnumerable<Position> GetPositions(string accountAlias)
    {
        var adapter = _adapterFactory.GetAdapter(accountAlias);
        if (adapter == null)
        {
            return Enumerable.Empty<Position>();
        }

        // Note: This is a synchronous wrapper around an async method, which is not ideal for Blazor Server.
        // However, given the current architecture and interface constraints, we'll use .Result carefully.
        // Ideally, the Dashboard should be fully async.
        try
        {
            return adapter.GetPositionsAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching positions for {Alias}", accountAlias);
            return Enumerable.Empty<Position>();
        }
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
