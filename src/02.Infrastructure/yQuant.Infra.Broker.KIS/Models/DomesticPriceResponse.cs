using System.Text.Json.Serialization;

namespace yQuant.Infra.Broker.KIS.Models;

public class DomesticPriceResponse
{
    [JsonPropertyName("output")]
    public DomesticPriceDetail? Output { get; set; }
}

public class DomesticPriceDetail
{
    [JsonPropertyName("stck_prpr")]
    public string StckPrpr { get; set; } = string.Empty;

    [JsonPropertyName("prdy_vrss")]
    public string PrdyVrss { get; set; } = string.Empty;

    [JsonPropertyName("prdy_ctrt")]
    public string PrdyCtrt { get; set; } = string.Empty;
}
