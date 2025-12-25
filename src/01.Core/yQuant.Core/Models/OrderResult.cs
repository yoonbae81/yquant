namespace yQuant.Core.Models;

public record OrderResult
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? OrderId { get; init; } // Internal Order ID
    public string? BrokerOrderId { get; init; } // Broker assigned Order ID (ODNO)

    public static OrderResult Success(string message, string? brokerOrderId = null) => 
        new() { IsSuccess = true, Message = message, BrokerOrderId = brokerOrderId };

    public static OrderResult Failure(string message) => 
        new() { IsSuccess = false, Message = message };
}
