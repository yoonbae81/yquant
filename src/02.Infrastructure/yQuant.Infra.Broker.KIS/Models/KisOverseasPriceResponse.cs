using System.Text.Json.Serialization;

namespace yQuant.Infra.Trading.KIS.Models;

public class KisOverseasPriceResponse
{
    [JsonPropertyName("output")]
    public KisOverseasPriceDetail? Output { get; set; }
}

public class KisOverseasPriceDetail
{
    [JsonPropertyName("last")]
    public string Last { get; set; } = string.Empty; // 현재가

    [JsonPropertyName("diff")]
    public string Diff { get; set; } = string.Empty; // 전일 대비

    [JsonPropertyName("rate")]
    public string Rate { get; set; } = string.Empty; // 전일 대비율
}
