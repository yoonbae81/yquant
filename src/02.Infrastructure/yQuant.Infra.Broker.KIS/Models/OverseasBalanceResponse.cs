using System.Text.Json.Serialization;

namespace yQuant.Infra.Broker.KIS.Models;

public class OverseasBalanceResponse
{
    [JsonPropertyName("output1")]
    public List<OverseasBalanceDetail>? Output1 { get; set; }

    [JsonPropertyName("output2")]
    public List<OverseasBalanceSummary>? Output2 { get; set; }

    [JsonPropertyName("output3")]
    public OverseasBalanceOutput3? Output3 { get; set; }
}

public class OverseasBalanceDetail
{
    // Define properties if needed for individual holdings in balance view
    // For now, we might not use this if we use Positions API for holdings
    [JsonPropertyName("ovrs_pdno")]
    public string OvrsPdno { get; set; } = string.Empty;
}

public class OverseasBalanceSummary
{
    [JsonPropertyName("frcr_evlu_amt")]
    public decimal ForeignCash { get; set; }

    [JsonPropertyName("tot_evlu_amt")]
    public decimal TotalAssetAmount { get; set; }
    
    [JsonPropertyName("nass_amt")]
    public decimal WithdrawableCash { get; set; }

    [JsonPropertyName("frcr_use_psbl_amt")]
    public decimal FrcrUsePsblAmt { get; set; }

    [JsonPropertyName("frcr_dncl_amt_2")]
    public decimal FrcrDnclAmt2 { get; set; }

    [JsonPropertyName("crcy_cd")]
    public string? CrcyCd { get; set; }

    [JsonPropertyName("ovrs_crcy_cd")]
    public string? OvrsCrcyCd { get; set; }
}

public class OverseasBalanceOutput3
{
    [JsonPropertyName("dncl_amt")]
    public decimal DnclAmt { get; set; }
}
