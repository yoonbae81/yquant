using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using yQuant.Core.Models;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Persistence;

public class MariaDbTradeRepository : ITradeRepository
{
    private readonly MariaDbContext _context;
    private readonly ILogger<MariaDbTradeRepository> _logger;

    public MariaDbTradeRepository(MariaDbContext context, ILogger<MariaDbTradeRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SaveAsync(string accountAlias, TradeRecord trade)
    {
        var entity = TradeRecordEntity.FromModel(accountAlias, trade);

        var existing = await _context.Trades.FindAsync(entity.Id);
        if (existing != null)
        {
            _context.Entry(existing).CurrentValues.SetValues(entity);
        }
        else
        {
            await _context.Trades.AddAsync(entity);
        }

        await _context.SaveChangesAsync();
        _logger.LogDebug("Saved trade {TradeId} for account {Account}", trade.Id, accountAlias);
    }

    public async Task<IEnumerable<TradeRecord>> GetTradesByDateAsync(string accountAlias, DateOnly date)
    {
        var start = date.ToDateTime(TimeOnly.MinValue);
        var end = date.ToDateTime(TimeOnly.MaxValue);

        var entities = await _context.Trades
            .Where(t => t.AccountAlias == accountAlias && t.ExecutedAt >= start && t.ExecutedAt <= end)
            .OrderByDescending(t => t.ExecutedAt)
            .ToListAsync();

        return entities.Select(e => e.ToModel());
    }

    public async Task<IEnumerable<TradeRecord>> GetTradesByRangeAsync(string accountAlias, DateOnly startDate, DateOnly endDate)
    {
        var start = startDate.ToDateTime(TimeOnly.MinValue);
        var end = endDate.ToDateTime(TimeOnly.MaxValue);

        var entities = await _context.Trades
            .Where(t => t.AccountAlias == accountAlias && t.ExecutedAt >= start && t.ExecutedAt <= end)
            .OrderByDescending(t => t.ExecutedAt)
            .ToListAsync();

        return entities.Select(e => e.ToModel());
    }

    public async Task<IEnumerable<TradeRecord>> GetAllTradesAsync(string accountAlias)
    {
        var entities = await _context.Trades
            .Where(t => t.AccountAlias == accountAlias)
            .OrderByDescending(t => t.ExecutedAt)
            .ToListAsync();

        return entities.Select(e => e.ToModel());
    }
}
