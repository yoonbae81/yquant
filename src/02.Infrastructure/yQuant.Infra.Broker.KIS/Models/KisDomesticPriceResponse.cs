using System.Text.Json.Serialization;

namespace yQuant.Infra.Trading.KIS.Models;

public class KisDomesticPriceResponse
{
    [JsonPropertyName("output")]
    public KisDomesticPriceDetail? Output { get; set; }
}

public class KisDomesticPriceDetail
{
    [JsonPropertyName("stck_prpr")]
    public string StckPrpr { get; set; } = string.Empty; // 현재가

    [JsonPropertyName("prdy_vrss")]
    public string PrdyVrss { get; set; } = string.Empty; // 전일 대비

    [JsonPropertyName("prdy_ctrt")]
    public string PrdyCtrt { get; set; } = string.Empty; // 전일 대비율
}
