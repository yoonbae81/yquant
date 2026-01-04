using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using yQuant.Infra.Valkey.Interfaces;

namespace yQuant.Infra.Valkey.Services;

public class TokenValkeyService : ValkeyService, ITokenValkeyService
{
    public TokenValkeyService(IConnectionMultiplexer connectionMultiplexer, ILogger<TokenValkeyService> logger)
        : base(connectionMultiplexer, logger)
    {
    }
}
