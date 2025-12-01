using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Infra.Broker.KIS;
using yQuant.Infra.Redis.Interfaces;

namespace yQuant.App.Dashboard.Services;

public class RedisService : IHostedService, IDisposable
{
    private readonly ILogger<RedisService> _logger;
    private readonly IRedisService _redisService;
    private readonly KISAdapterFactory _kisAdapterFactory;

    public RedisService(
        ILogger<RedisService> logger,
        IRedisService redisService,
        KISAdapterFactory kisAdapterFactory)
    {
        _logger = logger;
        _redisService = redisService;
        _kisAdapterFactory = kisAdapterFactory;
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
        var aliases = _kisAdapterFactory.GetAvailableAccounts();
        var accounts = new List<Account>();
        foreach (var alias in aliases)
        {
            var account = _kisAdapterFactory.GetAccount(alias);
            if (account != null)
            {
                accounts.Add(account);
            }
        }
        return accounts;
    }

    public IEnumerable<Position> GetPositions(string accountAlias)
    {
        var adapter = _kisAdapterFactory.GetAdapter(accountAlias);
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
