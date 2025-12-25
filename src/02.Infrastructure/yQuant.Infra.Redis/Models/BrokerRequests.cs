using System;
using System.Text.Json.Serialization;
using yQuant.Core.Models;

namespace yQuant.Infra.Redis.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BrokerRequestType
{
    Ping,
    GetPrice,
    GetDeposit,
    GetPositions,
    PlaceOrder,
    GetAccounts
}

public class BrokerRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public BrokerRequestType Type { get; set; }
    public string Account { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string ResponseChannel { get; set; } = string.Empty;
    public bool ForceRefresh { get; set; } = false;
}

public class BrokerResponse
{
    public Guid RequestId { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
}
