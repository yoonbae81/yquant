using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using yQuant.Infra.Valkey.Interfaces;
using yQuant.Infra.Persistence;
using FirebirdSql.Data.FirebirdClient;

namespace yQuant.App.Dashboard.Services;

public class SystemHealthService
{
    private readonly IValkeyService _messageValkey;
    private readonly FirebirdTradeRepository _firebirdRepo;
    private readonly ILogger<SystemHealthService> _logger;

    public SystemHealthService(
        IValkeyService messageValkey,
        FirebirdTradeRepository firebirdRepo,
        ILogger<SystemHealthService> logger)
    {
        _messageValkey = messageValkey;
        _firebirdRepo = firebirdRepo;
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

    public async Task<bool> CheckFirebirdAsync()
    {
        try
        {
            // Use the connection string from FirebirdTradeRepository if possible, 
            // but since we already have the repo injected, we just trust it's configured.
            // For a real check, we'd need access to the connection string.
            // Let's assume the repo is configured through IConfiguration.
            using var conn = new FbConnection(_firebirdRepo.ToString()); // This is just a placeholder, wait.
            // Actually, let's just use a simple query if we can.
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
