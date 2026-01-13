namespace yQuant.Infra.Broker.KIS;

public static class KisConstants
{
    // Order Divisions
    public const string OrderDivision_Limit = "00";
    public const string OrderDivision_Market = "01";

    // Inquiry Divisions
    public const string InquiryDivision_Asset = "02";
    public const string InquiryUnit_Asset = "01";

    // Other Flags
    public const string Flag_No = "N";
    public const string Flag_Yes = "Y";

    // Process Divisions
    public const string ProcessDivision_Main = "00"; // General/Main process

    // Overseas Market Codes
    public const string MarketCode_General = "00";

    // Currency Codes
    public const string Currency_USD = "USD";
    public const string Currency_HKD = "HKD";
    public const string Currency_CNY = "CNY";
    public const string Currency_JPY = "JPY";
    public const string Currency_VND = "VND";
    public const string Currency_KRW = "KRW";

    // Buy/Sell Division
    public const string BuySell_Sell = "01";
    public const string BuySell_Buy = "02";

    // Inquiry Division for Open Orders
    public const string OpenOrder_Inquiry_Order = "0"; // Order unit
    public const string OpenOrder_Inquiry_All = "00"; // All
    public const string OpenOrder_Inquiry_Unexecuted = "01"; // Unexecuted
}
