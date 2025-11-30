using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace yQuant.Infra.Broker.KIS.Models;

public class OverseasPositionsResponse
{
    [JsonPropertyName("output1")]
    public List<OverseasPosition>? Output1 { get; set; }
}

public class OverseasPosition
{
    [JsonPropertyName("ovrs_pdno")]
    public string OvrsPdno { get; set; } = string.Empty;
    [JsonPropertyName("ovrs_cblc_qty")]
    public decimal OvrsBuyQty { get; set; }
    [JsonPropertyName("ord_psbl_qty")]
    public decimal SellableQty { get; set; }
    [JsonPropertyName("avg_prch_prc")]
    public decimal AvgPrc { get; set; }
    [JsonPropertyName("now_pric2")]
    public decimal LastPrice { get; set; }
}
