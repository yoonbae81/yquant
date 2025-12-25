using System.Text.Json.Serialization;

namespace yQuant.Infra.Broker.KIS.Models;

public class HashkeyResponse
{
    [JsonPropertyName("HASH")]
    public string HASH { get; set; } = string.Empty;

    [JsonPropertyName("BODY")]
    public object? BODY { get; set; }
}
