using yQuant.Core.Models;

namespace yQuant.Infra.Broker.KIS;

public interface IKISClient
{
    Account Account { get; }
    Task EnsureConnectedAsync();
    Task<TResponse?> ExecuteAsync<TResponse>(string endpointName, object? body = null, Dictionary<string, string>? queryParams = null, Dictionary<string, string>? headers = null, string? trIdVariant = null);
    Task InvalidateTokenAsync();
}
