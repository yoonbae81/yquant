using System.Text.Json.Serialization;

namespace yQuant.Infra.Trading.KIS.Models;

public class KisDomesticBalanceResponse
{
    [JsonPropertyName("output1")]
    public List<KisDomesticPosition>? Output1 { get; set; }

    [JsonPropertyName("output2")]
    public List<KisDomesticBalanceDetail>? Output2 { get; set; }
}

public class KisDomesticPosition
{
    [JsonPropertyName("pdno")]
    public string Pdno { get; set; } = string.Empty; // 종목번호

    [JsonPropertyName("prdt_name")]
    public string PrdtName { get; set; } = string.Empty; // 상품명

    [JsonPropertyName("hldg_qty")]
    public decimal HldgQty { get; set; } // 보유수량

    [JsonPropertyName("pchs_avg_pric")]
    public decimal PchsAvgPric { get; set; } // 매입평균가격

    [JsonPropertyName("prpr")]
    public decimal Prpr { get; set; } // 현재가
}

public class KisDomesticBalanceDetail
{
    [JsonPropertyName("dnca_tot_amt")]
    public decimal DncaTotAmt { get; set; } // 예수금총금액

    [JsonPropertyName("ord_psbl_cash")]
    public decimal OrdPsblCash { get; set; } // 주문가능현금
}
