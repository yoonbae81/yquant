namespace yQuant.Core.Ports.Output.Infrastructure;

public class TokenCacheEntry
{
    public required string Token { get; set; }
    public required DateTime Expiration { get; set; }
}

public interface IKisTokenRepository
{
    Task<TokenCacheEntry?> GetTokenAsync(string accountAlias);
    Task SaveTokenAsync(string accountAlias, TokenCacheEntry entry);
    Task DeleteTokenAsync(string accountAlias);
}
