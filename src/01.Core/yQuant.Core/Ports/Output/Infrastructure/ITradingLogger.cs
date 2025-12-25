using System;
using System.Threading.Tasks;
using yQuant.Core.Models;

namespace yQuant.Core.Ports.Output.Infrastructure
{
    public interface ITradingLogger
    {
        // 1. Signal Logging
        // Webhook 앱에서 호출. timeframe은 Signal 객체에 없으므로 별도 파라미터 혹은 Source 필드 활용
        Task LogSignalAsync(Signal signal, string timeframe = "1d");

        // 2. Order Logging
        // BrokerGateway에서 주문 실행 시 호출
        Task LogOrderAsync(Order order);

        // 2.1. Order Failure Logging
        // BrokerGateway에서 주문 거부/실패 시 호출
        Task LogOrderFailureAsync(Order order, string reason);

        // 3. Account Error Logging
        // BrokerGateway에서 특정 계좌 관련 에러 발생 시 호출
        Task LogAccountErrorAsync(string accountAlias, Exception ex, string context);

        // 4. Daily Summary Logging
        // 일별 성과 집계 시 호출
        Task LogReportAsync(string accountAlias, PerformanceLog summary);
    }
}
