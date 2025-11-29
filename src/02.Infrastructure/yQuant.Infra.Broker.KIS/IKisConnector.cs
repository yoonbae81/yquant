using yQuant.Infra.Trading.KIS.Models;

namespace yQuant.Infra.Broker.KIS;

public interface IKisConnector
{
    Task EnsureConnectedAsync();
    Task<TResponse?> ExecuteAsync<TResponse>(string endpointName, object? body = null, Dictionary<string, string>? queryParams = null, Dictionary<string, string>? headers = null, string? trIdVariant = null);
    Task InvalidateTokenAsync();
}
