using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using yQuant.Infra.Valkey.Interfaces;

namespace yQuant.Infra.Valkey.Services;

public class StorageValkeyService : ValkeyService, IStorageValkeyService
{
    public StorageValkeyService(IConnectionMultiplexer connectionMultiplexer, ILogger<StorageValkeyService> logger)
        : base(connectionMultiplexer, logger)
    {
    }
}
