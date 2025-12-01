namespace yQuant.Core.Models;

public enum CountryCode
{
    KR,
    US,
    VN,
    HK,
    CN,
    JP
}

public enum CurrencyType
{
    KRW,
    USD,
    CNY,
    JPY,
    HKD,
    VND
}

public enum ExchangeCode
{
    NASDAQ,
    NYSE,
    AMEX,
    HKEX,
    SSE,
    SZSE,
    TSE,
    HNX,
    HOSE,
    KRX,
    KOSDAQ,
    KOSPI
}

public enum OrderAction
{
    Buy,
    Sell
}

public enum OrderType
{
    Limit,
    Market
}