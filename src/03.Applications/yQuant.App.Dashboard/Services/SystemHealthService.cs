using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using yQuant.Infra.Valkey.Interfaces;
using yQuant.Infra.Persistence;

namespace yQuant.App.Dashboard.Services;

public class SystemHealthService
{
    private readonly IValkeyService _messageValkey;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SystemHealthService> _logger;

    public SystemHealthService(
        IValkeyService messageValkey,
        IServiceScopeFactory scopeFactory,
        ILogger<SystemHealthService> logger)
    {
        _messageValkey = messageValkey;
        _scopeFactory = scopeFactory;
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

    public async Task<bool> CheckMariaDbAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MariaDbContext>();
            return await dbContext.Database.CanConnectAsync();
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
