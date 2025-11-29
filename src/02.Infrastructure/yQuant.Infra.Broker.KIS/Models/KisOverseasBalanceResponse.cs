using System.Text.Json.Serialization;

namespace yQuant.Infra.Trading.KIS.Models;

public class KisOverseasBalanceResponse
{
    [JsonPropertyName("output1")]
    public List<KisOverseasBalanceDetail>? Output1 { get; set; }

    [JsonPropertyName("output2")]
    public KisOverseasBalanceSummary? Output2 { get; set; }
}

public class KisOverseasBalanceDetail
{
    // Define properties if needed for individual holdings in balance view
    // For now, we might not use this if we use Positions API for holdings
    [JsonPropertyName("ovrs_pdno")]
    public string OvrsPdno { get; set; } = string.Empty;
}

public class KisOverseasBalanceSummary
{
    [JsonPropertyName("frcr_evlu_amt")] // 외화 평가 금액
    public decimal ForeignCash { get; set; }

    [JsonPropertyName("tot_evlu_amt")] // 총 평가 금액
    public decimal TotalAssetAmount { get; set; }
    
    [JsonPropertyName("nass_amt")] // 예수금
    public decimal WithdrawableCash { get; set; }
}
