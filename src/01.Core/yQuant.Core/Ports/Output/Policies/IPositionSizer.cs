using yQuant.Core.Models;

namespace yQuant.Core.Ports.Output.Policies;

public interface IPositionSizer
{
    /// <summary>
    /// 신호와 계좌 정보를 기반으로 적정 주문 수량 계산 (Position Sizing)
    /// </summary>
    /// <param name="signal">수신된 매매 신호 (Exchange 정보 포함)</param>
    /// <param name="account">현재 계좌 상태 (예수금, 포지션 포함)</param>
    /// <returns>수량이 계산된 주문 객체 (Order)</returns>
    Order? CalculatePositionSize(Signal signal, Account account);

    /// <summary>
    /// (옵션) 주문 집행 전 최종 리스크 검증 (Pre-Trade Validation)
    /// </summary>
    bool ValidateOrder(Order order, Account account, out string failureReason);
}
