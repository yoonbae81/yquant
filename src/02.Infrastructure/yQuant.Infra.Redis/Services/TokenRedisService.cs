using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using yQuant.Infra.Redis.Interfaces;

namespace yQuant.Infra.Redis.Services;

public class TokenRedisService : RedisService, ITokenRedisService
{
    public TokenRedisService(IConnectionMultiplexer connectionMultiplexer, ILogger<TokenRedisService> logger)
        : base(connectionMultiplexer, logger)
    {
    }
}
