namespace yQuant.Infra.Trading.KIS.Models;

public class KisDomesticOpenOrdersResponse
{
    public string RtCd { get; set; } = string.Empty;
    public string Msg1 { get; set; } = string.Empty;
    public List<KisDomesticOpenOrder> Output1 { get; set; } = new();
}

public class KisDomesticOpenOrder
{
    public string Pdno { get; set; } = string.Empty; // Ticker
    public string PrdtName { get; set; } = string.Empty; // Name
    public string OrdDtm { get; set; } = string.Empty; // Order Time
    public string OrdNo { get; set; } = string.Empty; // Order Number
    public string OrgOrdNo { get; set; } = string.Empty; // Original Order Number
    public string SllBuyDvsnCd { get; set; } = string.Empty; // Buy/Sell (01: Sell, 02: Buy)
    public string OrdQty { get; set; } = string.Empty; // Order Qty
    public string OrdUnpr { get; set; } = string.Empty; // Order Price
    public string RmnQty { get; set; } = string.Empty; // Remaining Qty
}

public class KisOverseasOpenOrdersResponse
{
    public string RtCd { get; set; } = string.Empty;
    public string Msg1 { get; set; } = string.Empty;
    public List<KisOverseasOpenOrder> Output { get; set; } = new();
}

public class KisOverseasOpenOrder
{
    public string Pdno { get; set; } = string.Empty; // Ticker
    public string PrdtName { get; set; } = string.Empty; // Name
    public string OrdDt { get; set; } = string.Empty; // Order Date
    public string OrdGno { get; set; } = string.Empty; // Order Number
    public string SllBuyDvsnCd { get; set; } = string.Empty; // Buy/Sell (01: Sell, 02: Buy)
    public string FtOrdQty { get; set; } = string.Empty; // Order Qty
    public string FtOrdUnpr3 { get; set; } = string.Empty; // Order Price
    public string RmnQty { get; set; } = string.Empty; // Remaining Qty
}
