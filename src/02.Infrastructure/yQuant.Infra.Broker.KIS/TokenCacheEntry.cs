namespace yQuant.Infra.Broker.KIS;

public class TokenCacheEntry
{
    public required string Token { get; set; }
    public required DateTime Expiration { get; set; }
}
