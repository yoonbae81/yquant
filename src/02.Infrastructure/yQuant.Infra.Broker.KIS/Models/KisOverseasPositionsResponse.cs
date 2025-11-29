using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace yQuant.Infra.Trading.KIS.Models;

public class KisOverseasPositionsResponse
{
    [JsonPropertyName("output1")]
    public List<KisOverseasPosition>? Output1 { get; set; }
}

public class KisOverseasPosition
{
    [JsonPropertyName("ovrs_pdno")] // 해외 종목 코드
    public string OvrsPdno { get; set; } = string.Empty;
    [JsonPropertyName("ovrs_cblc_qty")] // 해외 매수 잔고 수량
    public decimal OvrsBuyQty { get; set; }
    [JsonPropertyName("ord_psbl_qty")] // 매도 가능 수량
    public decimal SellableQty { get; set; }
    [JsonPropertyName("avg_prch_prc")] // 평균 매입 단가
    public decimal AvgPrc { get; set; }
    [JsonPropertyName("now_pric2")] // 현재가 (Note: API field might vary, checking docs or assuming 'now_pric2' or 'last_price')
    public decimal LastPrice { get; set; }
}
