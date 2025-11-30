using System.Text.Json.Serialization;

namespace yQuant.Infra.Broker.KIS.Models;

public class OverseasPriceResponse
{
    [JsonPropertyName("output")]
    public OverseasPriceDetail? Output { get; set; }
}

public class OverseasPriceDetail
{
    [JsonPropertyName("last")]
    public string Last { get; set; } = string.Empty;

    [JsonPropertyName("diff")]
    public string Diff { get; set; } = string.Empty;

    [JsonPropertyName("rate")]
    public string Rate { get; set; } = string.Empty;
}
