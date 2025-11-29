using System.Text.Json.Serialization;

namespace yQuant.Infra.Trading.KIS.Models;

public class KisOverseasBalanceResponse
{
    [JsonPropertyName("output1")]
    public List<KisOverseasBalanceDetail>? Output1 { get; set; }

    [JsonPropertyName("output2")]
    public List<KisOverseasBalanceSummary>? Output2 { get; set; }

    [JsonPropertyName("output3")]
    public KisOverseasBalanceOutput3? Output3 { get; set; }
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

    [JsonPropertyName("frcr_use_psbl_amt")] // 외화 사용 가능 금액
    public decimal FrcrUsePsblAmt { get; set; }

    [JsonPropertyName("frcr_dncl_amt_2")] // 외화 예수금 2
    public decimal FrcrDnclAmt2 { get; set; }

    [JsonPropertyName("crcy_cd")] // 통화 코드
    public string? CrcyCd { get; set; }

    [JsonPropertyName("ovrs_crcy_cd")] // 해외 통화 코드
    public string? OvrsCrcyCd { get; set; }
}

public class KisOverseasBalanceOutput3
{
    [JsonPropertyName("dncl_amt")] // 외화 예수금
    public decimal DnclAmt { get; set; }
}
