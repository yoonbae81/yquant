using System.Text.Json.Serialization;

namespace yQuant.Infra.Broker.KIS.Models;

public class DomesticBalanceResponse
{
    [JsonPropertyName("output1")]
    public List<DomesticPosition>? Output1 { get; set; }

    [JsonPropertyName("output2")]
    public List<DomesticBalanceDetail>? Output2 { get; set; }
}

public class DomesticPosition
{
    [JsonPropertyName("pdno")]
    public string Pdno { get; set; } = string.Empty;

    [JsonPropertyName("prdt_name")]
    public string PrdtName { get; set; } = string.Empty;

    [JsonPropertyName("hldg_qty")]
    public decimal HldgQty { get; set; }

    [JsonPropertyName("pchs_avg_pric")]
    public decimal PchsAvgPric { get; set; }

    [JsonPropertyName("prpr")]
    public decimal Prpr { get; set; }
}

public class DomesticBalanceDetail
{
    [JsonPropertyName("dnca_tot_amt")]
    public decimal DncaTotAmt { get; set; }

    [JsonPropertyName("ord_psbl_cash")]
    public decimal OrdPsblCash { get; set; }
}
