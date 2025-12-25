using System.Text.Json.Serialization;

namespace yQuant.Infra.Broker.KIS.Models;

public class OverseasOrderResponse
{
    [JsonPropertyName("rt_cd")]
    public string RtCd { get; set; } = string.Empty;

    [JsonPropertyName("msg1")]
    public string Msg1 { get; set; } = string.Empty;

    [JsonPropertyName("output")]
    public OverseasOrderOutput? Output { get; set; }
}

public class OverseasOrderOutput
{
    [JsonPropertyName("ODNO")]
    public string Odno { get; set; } = string.Empty;
}
