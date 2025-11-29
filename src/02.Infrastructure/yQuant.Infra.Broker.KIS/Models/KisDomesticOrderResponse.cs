using System.Text.Json.Serialization;

namespace yQuant.Infra.Trading.KIS.Models;

public class KisDomesticOrderResponse
{
    [JsonPropertyName("rt_cd")]
    public string RtCd { get; set; } = string.Empty;

    [JsonPropertyName("msg1")]
    public string Msg1 { get; set; } = string.Empty;

    [JsonPropertyName("output")]
    public KisDomesticOrderOutput? Output { get; set; }
}

public class KisDomesticOrderOutput
{
    [JsonPropertyName("KRX_FWDG_ORD_ORGNO")]
    public string KrxFwdgOrdOrgno { get; set; } = string.Empty;

    [JsonPropertyName("ODNO")]
    public string Odno { get; set; } = string.Empty;

    [JsonPropertyName("ORD_TMD")]
    public string OrdTmd { get; set; } = string.Empty;
}
