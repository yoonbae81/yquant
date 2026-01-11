using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Valkey.Services;

public class ValkeyScheduledOrderRepository : IScheduledOrderRepository
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ValkeyScheduledOrderRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);

    public ValkeyScheduledOrderRepository(IConnectionMultiplexer redis, ILogger<ValkeyScheduledOrderRepository> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<IEnumerable<ScheduledOrder>> GetAllAsync(string accountAlias)
    {
        var db = _redis.GetDatabase();
        var key = GetKey(accountAlias);
        var json = await db.StringGetAsync(key);

        if (!json.HasValue)
            return Enumerable.Empty<ScheduledOrder>();

        return JsonSerializer.Deserialize<List<ScheduledOrder>>(json.ToString(), _jsonOptions)
               ?? Enumerable.Empty<ScheduledOrder>();
    }

    public async Task ProcessOrdersAsync(string accountAlias, Func<List<ScheduledOrder>, Task<bool>> processor, bool waitForLock = false)
    {
        var db = _redis.GetDatabase();
        var lockKey = GetLockKey(accountAlias);
        var token = Guid.NewGuid().ToString();
        var acquired = false;

        // Retry logic
        int retries = waitForLock ? 20 : 1;
        TimeSpan delay = TimeSpan.FromMilliseconds(250);

        for (int i = 0; i < retries; i++)
        {
            if (await db.LockTakeAsync(lockKey, token, _lockTimeout))
            {
                acquired = true;
                break;
            }
            if (waitForLock && i < retries - 1)
                await Task.Delay(delay);
        }

        if (!acquired)
        {
            if (waitForLock)
            {
                _logger.LogError("Could not acquire lock for account {Account} after {Retries} retries", accountAlias, retries);
                throw new TimeoutException($"Could not acquire lock for account {accountAlias}");
            }

            _logger.LogWarning("Could not acquire lock for account {Account}, skipping processing", accountAlias);
            return;
        }

        try
        {
            var orders = (await GetAllAsync(accountAlias)).ToList();
            var shouldSave = await processor(orders);
            if (shouldSave)
            {
                await SaveInternalAsync(db, accountAlias, orders);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing orders for {Account}", accountAlias);
            throw;
        }
        finally
        {
            await db.LockReleaseAsync(lockKey, token);
        }
    }

    public async Task AddOrUpdateAsync(ScheduledOrder order)
    {
        await ProcessOrdersAsync(order.AccountAlias, async (orders) =>
        {
            var existing = orders.FirstOrDefault(o => o.Id == order.Id);
            if (existing != null)
            {
                orders.Remove(existing);
            }
            orders.Add(order);
            return await Task.FromResult(true);
        }, waitForLock: true);
    }

    public async Task RemoveAsync(string accountAlias, Guid orderId)
    {
        await ProcessOrdersAsync(accountAlias, async (orders) =>
        {
            var existing = orders.FirstOrDefault(o => o.Id == orderId);
            if (existing != null)
            {
                orders.Remove(existing);
                return await Task.FromResult(true);
            }
            return await Task.FromResult(false);
        }, waitForLock: true);
    }

    private async Task SaveInternalAsync(IDatabase db, string accountAlias, List<ScheduledOrder> orders)
    {
        var key = GetKey(accountAlias);
        if (orders.Count == 0)
        {
            await db.KeyDeleteAsync(key);
        }
        else
        {
            var json = JsonSerializer.Serialize(orders, _jsonOptions);
            await db.StringSetAsync(key, json);
        }
    }

    private string GetKey(string accountAlias) => $"scheduled:{accountAlias}";
    private string GetLockKey(string accountAlias) => $"scheduled:lock:{accountAlias}";
}
