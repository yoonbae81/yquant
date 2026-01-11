using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using yQuant.Core.Ports.Output.Infrastructure;

namespace yQuant.Infra.Persistence;

public class MariaDbKisTokenRepository : IKisTokenRepository
{
    private readonly MariaDbContext _context;
    private readonly ILogger<MariaDbKisTokenRepository> _logger;

    public MariaDbKisTokenRepository(MariaDbContext context, ILogger<MariaDbKisTokenRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TokenCacheEntry?> GetTokenAsync(string accountAlias)
    {
        var entity = await _context.Tokens
            .Where(t => t.AccountAlias == accountAlias && t.Broker == "KIS")
            .Select(t => new TokenCacheEntry
            {
                Token = t.Token,
                Expiration = t.Expiration
            })
            .FirstOrDefaultAsync();

        return entity;
    }

    public async Task SaveTokenAsync(string accountAlias, TokenCacheEntry entry)
    {
        var entity = await _context.Tokens.FindAsync(accountAlias);

        if (entity != null)
        {
            entity.Token = entry.Token;
            entity.Expiration = entry.Expiration;
            entity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            await _context.Tokens.AddAsync(new KisTokenEntity
            {
                AccountAlias = accountAlias,
                Broker = "KIS",
                Token = entry.Token,
                Expiration = entry.Expiration,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        _logger.LogDebug("Saved KIS token for account {Account}", accountAlias);
    }

    public async Task DeleteTokenAsync(string accountAlias)
    {
        var entity = await _context.Tokens
            .Where(t => t.AccountAlias == accountAlias && t.Broker == "KIS")
            .FirstOrDefaultAsync();

        if (entity != null)
        {
            _context.Tokens.Remove(entity);
            await _context.SaveChangesAsync();
            _logger.LogDebug("Deleted KIS token for account {Account}", accountAlias);
        }
    }
}
