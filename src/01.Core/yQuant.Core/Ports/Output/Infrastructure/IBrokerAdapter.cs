using System.Collections.Generic;
using System.Threading.Tasks;
using yQuant.Core.Models;

namespace yQuant.Core.Ports.Output.Infrastructure;

public interface IBrokerAdapter
{
    // 주문 실행
    Task<OrderResult> PlaceOrderAsync(Order order, string accountNumber);

    // 계좌 잔고 조회 (예수금, 총자산 등)
    Task<Account> GetAccountStateAsync(string accountNumber);

    // 보유 종목 리스트 조회
    Task<List<Position>> GetPositionsAsync(string accountNumber);

    // 세션/토큰 관리 (필요 시)
    Task EnsureConnectedAsync();

    // 미체결 주문 내역 조회
    Task<IEnumerable<Order>> GetOpenOrdersAsync(string accountNumber);

    // 현재가 및 등락률 조회
    Task<PriceInfo> GetPriceAsync(string ticker);
}
