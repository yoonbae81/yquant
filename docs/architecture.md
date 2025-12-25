# Architecture & Design Principles

## 1. Hexagonal Architecture (Ports and Adapters)
yQuant는 **헥사고날 아키텍처(Hexagonal Architecture)**를 채택하여 도메인 로직의 순수성을 보장하고 외부 기술(UI, DB, API 등)로부터의 독립성을 확보

### 1.1. 핵심 원칙
- **의존성 역전 원칙 (DIP)**: 고수준 모듈(Core)은 저수준 모듈(Infra)에 의존하지 않음. 둘 다 추상화(Port)에 의존
- **도메인 중심 설계**: 비즈니스 로직은 외부 환경의 변화에 영향을 받지 않아야 함
- **테스트 용이성**: 외부 의존성을 Mocking하여 도메인 로직을 단위 테스트하기 용이

## 2. Layer Structure

### 2.1. Core Layer (The Law)
시스템의 가장 안쪽에 위치하며, 비즈니스 로직과 도메인 모델을 포함
- **Models**: `Signal`, `Order`, `Position` 등 도메인 엔티티 및 값 객체(VO)
- **Ports**:
    - **Input Ports (Use Cases)**: 외부에서 도메인에 요청하는 인터페이스 (e.g., `IOrderCompositionUseCase`)
    - **Output Ports**: 도메인이 외부 서비스를 사용하기 위한 인터페이스 (e.g., `IBrokerAdapter`, `IPositionSizer`)
- **Services**: Input Port를 구현하여 도메인 로직을 오케스트레이션하는 서비스 클래스

### 2.2. Infrastructure Layer (The Tools)
Core Layer의 Output Port를 구현하는 어댑터들이 위치
- **Broker Adapters**: KIS(한국투자증권) 등 증권사 API 연동 구현체
- **Messaging Adapters**: Redis Pub/Sub 통신 구현체
- **Notification Adapters**: Discord, Telegram 알림 발송 구현체 (`yQuant.Infra.Notification.*`)
- **Reporting Adapters**: 성과 데이터(`yQuant.Infra.Reporting`) 및 QuantStats 지표 산출(`yQuant.Infra.Reporting.QuantStats`) 로깅 구현체

### 2.3. Policy Layer (The Logic Plugins)
전략적 의사결정을 담당하는 로직을 관리하는 계층
- **Market Rules**: 거래소별(KRX, NYSE, NASDAQ 등) 운영 규칙. 현재는 `appsettings.json` 설정을 통해 동적으로 구성됨
- **Position Sizing**: 자금 관리 및 주문 수량 계산 알고리즘 (`yQuant.Policies.Sizing`)
- 각 Policy는 인터페이스 기반으로 구현되어 상호 교체 및 확장이 가능

### 2.4. Application Layer (The Runners)
시스템을 실행하는 진입점(Entry Point). Core, Infra, Policy를 조립(Composition)하여 실행 가능한 프로세스 생성
- **Webhook**: 외부 신호 수신용 경량 웹 서버
- **OrderManager**: 신호 수신, 주문 생성, 예약 주문 및 청산 관리 엔진
- **BrokerGateway**: 증권사 연결 관리, 주문 라우팅 및 상태 동기화
- **Web**: Blazor Server 기반 대시보드 및 리포팅 UI
- **Console**: 수동 제어, 테스트, 종목 마스터 데이터 동기화용 CLI
- **Notifier**: Redis 이벤트를 구독하여 소셜 알림 플랫폼으로 전파하는 전용 서비스

## 3. Data Flow
1. **Signal Ingestion**: TradingView Webhook → `yQuant.App.Webhook` → Redis Pub/Sub (`signal`)
2. **Order Composition**: Redis (`signal`) → `yQuant.App.OrderManager` → `OrderManagementService` → Config-based `MarketRule` & `PositionSizer` → Redis (`order`)
3. **Order Execution**: Redis (`order`) → `yQuant.App.BrokerGateway` → `KISAdapter` → Broker API
4. **Execution Feedback**: Broker API → `BrokerGateway` → Redis (`execution`) → Notifier / Web / Discord / Telegram
5. **Performance Tracking**: `BrokerGateway` → Redis (`execution`) → `OrderManager` / `System` → `IPerformanceRepository` (Local CSV/JSON)
6. **Reporting & Analysis**: `yQuant.App.Web` → `QuantStatsService` → `PerformanceRepository` 조회 → 리포트 시각화 (Equity Curve 등)
7. **Master Data Sync**: `Console (catalog command)` → `yQuant.Infra.Master.KIS` → Broker API → Redis Cache (`stock:{Ticker}`)
8. **Health Monitoring**: 각 앱 → Redis (`heartbeat`) → `yQuant.App.Web` (Service Status Dashboard)
