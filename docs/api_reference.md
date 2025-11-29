# API Reference

yQuant 시스템은 내부적으로 Redis Pub/Sub 메시징을 사용하며, 외부 증권사 연동은 한국투자증권(KIS) Open API를 기반으로 함

## Broker API (Korea Investment & Securities)

본 프로젝트는 한국투자증권의 Open API를 사용하여 시세 조회 및 주문 수행. 상세한 API 명세는 아래 공식 문서 참조

- **한국투자증권 개발자 센터**: [https://apiportal.koreainvestment.com/](https://apiportal.koreainvestment.com/)
- **API 문서 (Wiki)**: [https://wikidocs.net/book/1173](https://wikidocs.net/book/1173)

## Internal Messaging Protocol (Redis)

시스템 내부 컴포넌트 간 통신은 Redis Pub/Sub 사용

### Pub/Sub Channels
- `yquant:signals`: TradingView Webhook에서 수신된 원본 신호 (Webhook → OrderComposer)
- `yquant:orders`: 검증 및 생성이 완료된 실행 가능한 주문 (OrderComposer → BrokerGateway)
- `yquant:executions`: 증권사에서 체결된 결과 통보 (BrokerGateway → Dashboard/Notification)
- `yquant:errors`: 시스템 에러 로그 (All Components → Logging)
- `yquant:performance`: 일간 성과 데이터 (BrokerGateway → QuantStats)

### Cache Keys
- `cache:account:{alias}`: 계좌별 자산 현황 스냅샷 (예수금, 총 자산, 보유 종목 수)
- `cache:position:{alias}:{ticker}`: 종목별 포지션 상세 (평균 단가, 보유 수량, 평가 손익)
- `cache:master:{exchange}:{ticker}`: 종목 마스터 데이터 (종목명, 거래소, 통화)
- `cache:price:{ticker}`: 실시간 현재가 정보

### Data Models
데이터 모델의 스키마는 `yQuant.Core.Models` 네임스페이스의 클래스 정의를 따름
- `Signal`: 매매 신호 (Exchange, Ticker, Action, Strength)
- `Order`: 실행 주문 (AccountAlias, Ticker, Qty, Price, OrderType)
- `Position`: 포지션 정보 (Ticker, Qty, AvgPrice, CurrentPrice, UnrealizedPnL)
- `Account`: 계좌 정보 (Alias, Deposits, Positions)
- `PerformanceLog`: 성과 기록 (Date, TotalAsset, DailyReturn, CumulativeReturn)
