using System.Text.Json.Serialization;

namespace yQuant.Infra.Trading.KIS.Models;

public class KisOverseasOrderResponse
{
    [JsonPropertyName("rt_cd")]
    public string RtCd { get; set; } = string.Empty;

    [JsonPropertyName("msg1")]
    public string Msg1 { get; set; } = string.Empty;

    [JsonPropertyName("output")]
    public KisOverseasOrderOutput? Output { get; set; }
}

public class KisOverseasOrderOutput
{
    [JsonPropertyName("ODNO")]
    public string Odno { get; set; } = string.Empty;
}
