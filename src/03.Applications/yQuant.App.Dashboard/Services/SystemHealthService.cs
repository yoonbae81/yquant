using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using yQuant.Infra.Valkey.Interfaces;

namespace yQuant.App.Dashboard.Services;

public class SystemHealthService
{
    private readonly IValkeyService _messageValkey;
    private readonly IStorageValkeyService _storageValkey;
    private readonly ILogger<SystemHealthService> _logger;

    public SystemHealthService(
        IValkeyService messageValkey,
        IStorageValkeyService storageValkey,
        ILogger<SystemHealthService> logger)
    {
        _messageValkey = messageValkey;
        _storageValkey = storageValkey;
        _logger = logger;
    }

    public async Task<bool> CheckMessageValkeyAsync()
    {
        try
        {
            var db = _messageValkey.Connection.GetDatabase();
            await db.PingAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CheckStorageValkeyAsync()
    {
        try
        {
            var db = _storageValkey.Connection.GetDatabase();
            await db.PingAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CheckServiceAsync(string serviceName)
    {
        try
        {
            // Heartbeats are stored in Message Valkey
            var db = _messageValkey.Connection.GetDatabase();
            var key = $"status:heartbeat:{serviceName}";
            return await db.KeyExistsAsync(key);
        }
        catch
        {
            return false;
        }
    }
}
