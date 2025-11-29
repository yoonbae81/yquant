using System;
using yQuant.Core.Models;

namespace yQuant.Core.Ports.Output.Policies;

public interface IMarketRule
{
    /// <summary>
    /// 정책이 해당 거래소를 처리할 수 있는지 확인 (N:1 맵핑 지원)
    /// </summary>
    /// <param name="exchange">TradingView Signal의 Exchange 값 (예: NASDAQ, KRX)</param>
    /// <returns>처리 가능 여부</returns>
    bool CanHandle(string exchange);

    /// <summary>
    /// 현재 시각 기준 시장 개장 여부 확인
    /// </summary>
    bool IsMarketOpen(DateTime timestamp);

    /// <summary>
    /// 해당 시장의 기준 통화 반환
    /// </summary>
    CurrencyType GetCurrency();
}
