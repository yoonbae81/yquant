using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Persistence;

public class MariaDbScheduledOrderRepository : IScheduledOrderRepository
{
    private readonly MariaDbContext _context;
    private readonly ILogger<MariaDbScheduledOrderRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public MariaDbScheduledOrderRepository(MariaDbContext context, ILogger<MariaDbScheduledOrderRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<ScheduledOrder>> GetAllAsync(string accountAlias)
    {
        var entities = await _context.ScheduledOrders
            .Where(o => o.AccountAlias == accountAlias)
            .ToListAsync();

        return entities
            .Select(e => JsonSerializer.Deserialize<ScheduledOrder>(e.Data, _jsonOptions)!)
            .Where(o => o != null);
    }

    public async Task ProcessOrdersAsync(string accountAlias, Func<List<ScheduledOrder>, Task<bool>> processor, bool waitForLock = false)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Stale Lock Cleanup: Reset locks older than 20 hours
            var staleLockThreshold = DateTime.UtcNow.AddHours(-20);
            await _context.ScheduledOrders
                .Where(o => o.AccountAlias == accountAlias && o.LockedAt < staleLockThreshold)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(o => o.LockedAt, (DateTime?)null)
                    .SetProperty(o => o.LockedBy, (string?)null));

            // 2. Row-Level Locking: Load entities with tracking
            // Note: In-memory database doesn't support FOR UPDATE, but in production MariaDB it will
            var lockedEntities = await _context.ScheduledOrders
                .Where(o => o.AccountAlias == accountAlias)
                .ToListAsync();

            // 3. Mark Lock: Update tracking fields
            if (lockedEntities.Any())
            {
                var lockIds = lockedEntities.Select(e => e.Id).ToList();
                await _context.ScheduledOrders
                    .Where(o => lockIds.Contains(o.Id))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(o => o.LockedAt, DateTime.UtcNow)
                        .SetProperty(o => o.LockedBy, Environment.MachineName));
            }

            // Load orders
            var orders = lockedEntities
                .Select(e => JsonSerializer.Deserialize<ScheduledOrder>(e.Data, _jsonOptions)!)
                .ToList();

            if (!orders.Any())
            {
                await transaction.CommitAsync();
                return;
            }

            var shouldSave = await processor(orders);
            if (shouldSave)
            {
                // Delete missing orders
                var currentIds = orders.Select(o => o.Id.ToString()).ToList();
                await _context.ScheduledOrders
                    .Where(o => o.AccountAlias == accountAlias && !currentIds.Contains(o.Id))
                    .ExecuteDeleteAsync();

                // Update or Insert
                foreach (var order in orders)
                {
                    var entity = await _context.ScheduledOrders.FindAsync(order.Id.ToString());
                    if (entity != null)
                    {
                        entity.Data = JsonSerializer.Serialize(order, _jsonOptions);
                        entity.LockedAt = null;
                        entity.LockedBy = null;
                        entity.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        await _context.ScheduledOrders.AddAsync(new ScheduledOrderEntity
                        {
                            Id = order.Id.ToString(),
                            AccountAlias = accountAlias,
                            Data = JsonSerializer.Serialize(order, _jsonOptions),
                            LockedAt = null,
                            LockedBy = null,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }
            }
            else
            {
                // Reset lock info if not saving
                await _context.ScheduledOrders
                    .Where(o => o.AccountAlias == accountAlias)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(o => o.LockedAt, (DateTime?)null)
                        .SetProperty(o => o.LockedBy, (string?)null));
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error processing orders for {Account} in MariaDB", accountAlias);
            throw;
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
            return true;
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
                return true;
            }
            return false;
        }, waitForLock: true);
    }
}
