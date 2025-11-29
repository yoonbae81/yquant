using System.Text.Json.Serialization;

namespace yQuant.Infra.Trading.KIS.Models;

public class KisHashkeyResponse
{
    [JsonPropertyName("HASH")]
    public string HASH { get; set; } = string.Empty;

    [JsonPropertyName("BODY")]
    public object? BODY { get; set; }
}
