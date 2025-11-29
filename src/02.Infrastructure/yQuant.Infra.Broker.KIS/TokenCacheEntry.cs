namespace yQuant.Infra.Broker.KIS;

internal class TokenCacheEntry
{
    public required string Token { get; set; }
    public required DateTime Expiration { get; set; }
}
