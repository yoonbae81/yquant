# Module Documentation

## 1. Core Layer (`src/01.Core`)

Core 계층은 시스템의 도메인 로직과 인터페이스를 정의하는 순수 영역. 외부 라이브러리에 대한 의존성 없음

### 1.1. Models (Domain Entities)
- **Signal**: 외부(TradingView)에서 수신된 매매 의도
    - `Exchange`: 라우팅의 핵심 키 (예: "KRX", "NASDAQ")
    - `Action`: 매수(Buy)/매도(Sell)
    - `Strength`: 신호 강도 (자금 관리 정책에 사용)
- **Order**: 실행 가능한 최종 주문 객체
    - `AccountAlias`: 주문을 집행할 계좌 Alias (예: "Main_Aggressive")
    - `Qty`: 자금 관리 규칙에 의해 계산된 수량
- **Account**: 계좌의 상태(예수금, 보유 종목)를 나타냄
    - `Deposits`: 통화별(KRW, USD) 예수금 잔고
    - `Positions`: 현재 보유 중인 종목 리스트

### 1.2. Ports (Interfaces)
- **Input Ports (Use Cases)**
    - `IOrderCompositionUseCase`: 신호를 받아 주문을 생성하는 메인 유스케이스
- **Output Ports (Infrastructure)**
    - `IBrokerAdapter`: 증권사 API와 통신하기 위한 추상화 (주문 전송, 잔고 조회)
    - `INotificationService`: 알림 발송을 위한 인터페이스
- **Output Ports (Policies)**
    - `IPositionSizer`: 리스크 관리 및 주문 수량 계산 정책
    - `IMarketRule`: 거래소별 운영 규칙(개장 시간, 통화 등) 정의. `CanHandle(exchange)` 메서드로 N:1 매핑 지원

### 1.3. Services
- **OrderCompositionService**: `IOrderCompositionUseCase`의 구현체
    - **Policy Selection**: 수신된 Signal의 Exchange를 기반으로 적절한 `IMarketRule` 선택
    - **Validation**: 시장 개장 여부(`IsMarketOpen`) 확인
    - **Sizing**: `IPositionSizer`를 호출하여 주문 수량 계산
    - **Publishing**: 완성된 Order를 생성하여 반환

---

## 2. Infrastructure Layer (`src/02.Infrastructure`)

Core의 인터페이스를 구현하고 실제 기술(DB, API, Network)을 다룸

### 2.1. yQuant.Infra.Broker.KIS (한국투자증권)
- **역할**: `IBrokerAdapter` 구현체
- **기능**:
    - **OAuth 2.0 인증**: Access Token의 발급 및 만료 시 자동 갱신 처리
    - **API 통합**: 한국 주식(KRX)과 미국 주식(NASDAQ 등)의 상이한 API 엔드포인트를 하나의 인터페이스로 통합
    - **Rate Limiting**: 초당 요청 제한(QPS)을 준수하여 API 호출 제어
    - **Token Caching**: Access Token을 메모리 및 로컬 파일(`%LocalAppData%/yQuant/KIS/tokens`)에 캐싱하여 재사용
    - **US Market Order Emulation**: 미국 주식 시장가 주문 미지원으로 인해, 현재가 기준 Buffer(매수 +5%, 매도 -5%)를 적용한 지정가 주문으로 에뮬레이션

### 2.2. yQuant.Infra.Middleware.Redis
- **역할**: 시스템의 신경망 역할을 하는 메시지 브로커 및 상태 저장소
- **Pub/Sub Channels**:
    - `yquant:signals`: Webhook에서 수신한 Raw Signal을 OrderComposer로 전달하는 Redis 채널
    - `yquant:orders`: OrderComposer가 생성한 Executable Order를 BrokerGateway로 전달하는 Redis 채널
    - `yquant:executions`: BrokerGateway가 체결 결과를 Dashboard 및 Notification 서비스로 전파하는 Redis 채널
- **Caching Keys**:
    - `account:{Alias}`: 계좌 정적 정보 (번호, 증권사)
    - `deposit:{Alias}`: 실시간 예수금 잔고
    - `position:{Alias}`: 실시간 보유 종목 포지션
    - `stock:{Ticker}`: 종목 정적 정보 및 실시간 시세

### 2.3. yQuant.Infra.Notification.Discord
- **역할**: `ITradingLogger` 및 `ISystemLogger` 구현체
- **기능**:
    - **Structured Logging**: Embed를 활용하여 가독성 높은 알림 메시지(체결, 에러, 요약) 전송
    - **Routing**: 전략(Strategy)이나 계좌(Account)별로 다른 Discord Webhook URL로 메시지를 분기 전송
    - **Fire-and-Forget**: 메인 트레이딩 로직에 영향을 주지 않도록 비동기 처리

### 2.4. yQuant.Infra.Notification.Telegram
- **역할**: `ITradingLogger` 및 `ISystemLogger` 구현체
- **기능**:
    - **Bot API**: Telegram Bot API를 통한 실시간 매매 체결 및 시스템 이벤트 알림
    - **Message Formatting**: Markdown 포맷을 활용한 구조화된 메시지 전송
    - **Non-blocking**: 비동기 처리로 트레이딩 로직에 영향 최소화

### 2.5. yQuant.Infra.Reporting.QuantStats
- **역할**: `IPerformanceRepository` 구현체
- **기능**:
    - **Performance Logging**: 일간 수익률 및 자산 변동 데이터를 파일 시스템에 영구 저장
    - **CSV Export**: QuantStats Python 라이브러리 호환 형식의 CSV 파일 자동 생성
    - **Data Aggregation**: Redis에서 실시간 성과 데이터를 수집하여 리포트 생성

---

## 3. Policy Layer (`src/04.Policies`)

Policy 계층은 거래소별 시장 규칙과 포지션 사이징 알고리즘을 플러그인 형태로 제공

### 3.1. Market Policies (거래소 규칙)

각 거래소별 특화된 운영 규칙을 `IMarketRule` 인터페이스로 구현

#### yQuant.Policies.Market.Korea
- **담당 거래소**: KRX (한국거래소)
- **주요 규칙**:
    - **개장 시간**: 평일 09:00-15:30 (KST)
    - **통화**: KRW
    - **호가 단위**: 가격대별 틱 사이즈 적용
    - **거래 제한**: 상한가/하한가 제도

#### yQuant.Policies.Market.UnitedStates
- **담당 거래소**: NYSE, NASDAQ, AMEX
- **주요 규칙**:
    - **개장 시간**: 평일 09:30-16:00 (EST/EDT)
    - **통화**: USD
    - **Pre/After Market**: 시간 외 거래 지원 여부 확인
    - **PDT Rule**: Pattern Day Trader 규칙 고려

#### yQuant.Policies.Market.China
- **담당 거래소**: SSE (상하이증권거래소), SZSE (선전증권거래소)
- **주요 규칙**:
    - **개장 시간**: 평일 09:30-15:00 (CST)
    - **통화**: CNY
    - **T+1 결제**: 당일 매수 익일 매도 가능

#### yQuant.Policies.Market.HongKong
- **담당 거래소**: HKEX (홍콩증권거래소)
- **주요 규칙**:
    - **개장 시간**: 평일 09:30-16:00 (HKT, 점심시간 12:00-13:00 제외)
    - **통화**: HKD
    - **Pre-Opening**: 09:00-09:30 프리오프닝 세션

#### yQuant.Policies.Market.Japan
- **담당 거래소**: TSE (도쿄증권거래소)
- **주요 규칙**:
    - **개장 시간**: 평일 09:00-15:00 (JST, 점심시간 11:30-12:30 제외)
    - **통화**: JPY
    - **호가 단위**: 가격대별 틱 사이즈 적용

#### yQuant.Policies.Market.Vietnam
- **담당 거래소**: HOSE (호치민증권거래소), HNX (하노이증권거래소)
- **주요 규칙**:
    - **개장 시간**: 평일 09:00-15:00 (ICT)
    - **통화**: VND
    - **가격 제한**: 일일 변동폭 제한

### 3.2. Position Sizing Policies

자금 관리 및 리스크 통제를 위한 주문 수량 계산 알고리즘

#### yQuant.Policies.Sizing.Basic
- **역할**: `IPositionSizer` 구현체
- **전략**:
    - **고정 비율**: 계좌 자산의 고정 비율(예: 2%)로 주문 금액 계산
    - **Signal Strength 반영**: 신호 강도에 따른 비중 조정
    - **최대 포지션 한도**: 단일 종목 최대 보유 비율 제한

---

## 4. Application Layer (`src/03.Applications`)

각 컴포넌트를 조립하여 실행되는 프로세스들

### 3.1. yQuant.App.Webhook (Signal Ingress)
- **유형**: ASP.NET Core Minimal API
- **역할**: TradingView Alert를 수신하여 내부 시스템으로 전달
- **보안**:
    - **IP Whitelist**: 허용된 IP 대역(TradingView 서버 등)에서의 요청만 수락
    - **Secret Key**: Payload 내의 Secret 값 검증
- **로직**:
    - JSON Payload를 `Signal` 객체로 변환 (수량 계산은 하지 않음)
    - Redis 채널 `yquant:signals`로 변환된 Signal 객체 발행(Publish)

### 3.2. yQuant.App.OrderComposer (Logic Engine)
- **유형**: Worker Service (Background Process)
- **역할**: Signal을 Order로 변환하는 두뇌 역할
- **프로세스**:
    1. **Signal 수신**: Redis 채널 `yquant:signals`를 구독(Subscribe)하여 메시지 수신
    2. **정책 적용**: Signal의 Exchange 정보에 맞는 `MarketRule` 선택 (예: NASDAQ -> US Policy)
    3. **상태 동기화**: Redis Cache(`deposit:{Alias}`, `position:{Alias}`)에서 현재 계좌 잔고 및 포지션 조회
    4. **Sizing**: `BasicPositionSizer`를 통해 자금 관리 규칙(2% 룰 등) 적용 및 수량 계산
    5. **Order 발행**: 유효한 주문 객체를 Redis 채널 `yquant:orders`로 발행(Publish)

### 3.3. yQuant.App.BrokerGateway (Execution Gateway)
- **유형**: Worker Service
- **역할**: 증권사와의 물리적 연결 전담
- **주요 기능**:
    - **Order Dispatching**: Redis 채널 `yquant:orders`를 구독(Subscribe)하고, 계좌 ID에 맞는 Broker Adapter로 주문 전송
    - **State Synchronization**: 주기적(예: 1초)으로 증권사 API를 폴링하여 잔고 및 포지션을 조회하고, Redis Cache(`deposit:{Alias}`, `position:{Alias}`) 최신화
    - **Error Handling**: 주문 실패나 네트워크 오류 발생 시 알림을 발송하고 로그 기록

### 3.4. yQuant.App.Console (Manual Control)
- **유형**: Console Application (CLI)
- **역할**: 비상 대응 및 테스트를 위한 수동 제어 도구
- **명령어**:
    - `price <ticker>`: 현재가 조회
    - `buy <ticker> <qty> [price]`: 매수 주문 (가격 미지정 시 시장가)
    - `sell <ticker> <qty> [price]`: 매도 주문 (가격 미지정 시 시장가)

### 3.5. yQuant.App.Dashboard (Web UI)
- **유형**: Blazor Server Application
- **역할**: 웹 기반 실시간 모니터링 및 제어 대시보드
- **주요 기능**:
    - **자산 현황**: Redis Cache(`account:{Alias}`, `deposit:{Alias}`, `position:{Alias}`)를 조회하여 실시간 예수금, 보유 종목, 평가손익 표시
    - **포지션 관리**: 보유 종목별 상세 정보 및 수익률 시각화
    - **수동 거래**: 웹 UI를 통한 즉시 매수/매도 주문 실행
    - **예약 주문**: 지정 시간에 자동 실행되는 예약 주문 설정

### 3.6. yQuant.App.StockMaster (Data Sync)
- **유형**: Worker Service
- **역할**: 종목 마스터 데이터 동기화 및 관리
- **주요 기능**:
    - **Master Data Sync**: 증권사 API로부터 거래 가능 종목 리스트 주기적 동기화
    - **Dual Mode**:
        - **Worker Mode**: 설정된 스케줄(`RunTime`)에 따라 백그라운드에서 주기적으로 동기화 수행
        - **CLI Mode**: 커맨드라인 인자(`--worker` 미지정)로 실행 시 즉시 동기화 수행 (전체 또는 특정 국가)
    - **Supported Exchanges**: KOSPI, KOSDAQ, NASDAQ, NYSE, AMEX, SSE, SZSE, TSE, HKEX, HNX, HOSE
    - **Redis Caching**: 종목 정보를 Redis에 캐싱하여 빠른 조회 지원
    - **Exchange Mapping**: 종목 코드와 거래소(Exchange) 매핑 정보 관리

### 3.7. yQuant.App.RedisVerifier (Diagnostics)
- **유형**: Console Application
- **역할**: Redis 연결 및 Pub/Sub 채널 동작 검증
- **주요 기능**:
    - **Connection Test**: Redis 서버 연결 상태 확인
    - **Channel Verification**: Pub/Sub 채널의 메시지 송수신 테스트
    - **Cache Inspection**: Redis에 저장된 캐시 데이터 조회 및 검증
