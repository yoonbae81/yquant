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
    - `IOrderManagementUseCase`: 신호를 받아 주문을 생성하는 메인 유스케이스
- **Output Ports (Infrastructure)**
    - `IBrokerAdapter`: 증권사 API와 통신하기 위한 추상화 (주문 전송, 잔고 조회)
    - `INotificationService`: 알림 발송을 위한 인터페이스
- **Output Ports (Policies)**
    - `IPositionSizer`: 리스크 관리 및 주문 수량 계산 정책
    - `IMarketRule`: 거래소별 운영 규칙(개장 시간, 통화 등) 정의. `CanHandle(exchange)` 메서드로 N:1 매핑 지원

### 1.3. Services
- **OrderManagementService**: `IOrderManagementUseCase`의 구현체
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

### 2.2. yQuant.Infra.Valkey (Valkey Messaging & Caching)
- **역할**: 시스템의 신경망 역할을 하는 메시지 브로커 및 상태 저장소
- **주요 컴포넌트**:
    - **ValkeyService**: Valkey 연결 관리 및 기본 작업
    - **ValkeyBrokerClient**: Pub/Sub 메시징 클라이언트
    - **HeartbeatService**: Valkey 연결 상태 모니터링
- **Pub/Sub Channels**:
    - `signal`: Webhook에서 수신한 Raw Signal을 OrderManager로 전달하는 Valkey 채널
    - `order`: OrderManager가 생성한 Executable Order를 BrokerGateway로 전달하는 Valkey 채널
    - `execution`: BrokerGateway가 체결 결과를 Web 및 Notification 서비스로 전파하는 Valkey 채널
- **Caching Keys**:
    - `account:index`: 계좌 인덱스 (Set)
    - `account:{Alias}`: 계좌 상세 정보 (Hash)
    - `deposit:{Alias}`: 실시간 예수금 잔고 (Hash)
    - `position:{Alias}`: 실시간 보유 종목 포지션 (Hash)
    - `stock:{Ticker}`: 종목 정적 정보 및 실시간 시세 (Hash)

### 2.3. yQuant.Infra.Notification
- **역할**: 알림 시스템의 기반 모델 및 전송 로직 추상화
- **하위 모듈**:
    - `.Discord`: Discord Webhook을 통한 알림 (Structured Logging 지원)
    - `.Telegram`: Telegram Bot API를 통한 알림
- **특징**: 
    - `NotificationPublisher`: Valkey를 통한 비동기 알림 발행 지원 (`common:notifications`)
    - `ISystemLogger`, `ITradingLogger` 인터페이스 구현

### 2.4. yQuant.Infra.Reporting
- **역할**: 트레이딩 성과 데이터 관리 및 분석 도구 연동
- **기능**:
    - **Performance Recording**: 일간 수익률, 자산 변동, 체결 이력을 로컬 파일(CSV/JSON)에 저장
    - **Data Repository**: `IPerformanceRepository`를 통해 영속성 관리

### 2.5. yQuant.Infra.Master.KIS
- **역할**: 한국투자증권에서 제공하는 종목 마스터 데이터 파일 다운로드 및 파싱
- **기능**:
    - FTP/HTTP를 통한 마스터 압축 파일 수신 및 자동 압축 해제
    - 종목코드, 한글명, 영문명, 상장상태 등 메타데이터 추출

---

## 3. Policy Layer (`src/04.Policies`)

Policy 계층은 거래소별 시장 규칙과 포지션 사이징 알고리즘을 플러그인 형태로 제공

### 3.1. Market Rules (Config-based)

기존의 DLL 플러그인 방식에서 `appsettings.json` 기반의 설정 방식으로 전환되었습니다. `ConfigurableMarketRule` 클래스가 JSON 설정을 읽어들여 각 시장의 운영 규칙을 동적으로 적용합니다.

**주요 설정 항목:**
- `IsActive`: 시장 활성화 여부
- `OpenTime` / `CloseTime`: 시장 운영 시간
- `Currency`: 기준 통화
- `TickSize`: 가격대별 호가 단위

### 3.2. Position Sizing Policies

자금 관리 및 리스크 통제를 위한 주문 수량 계산 알고리즘

#### yQuant.Policies.Sizing
- **역할**: `IPositionSizer` 구현체
- **전략**:
    - **고정 비율**: 계좌 자산의 고정 비율(예: 2%)로 주문 금액 계산
    - **Signal Strength 반영**: 신호 강도에 따른 비중 조정
    - **최대 포지션 한도**: 단일 종목 최대 보유 비율 제한

---

## 4. Application Layer (`src/03.Applications`)

각 컴포넌트를 조립하여 실행되는 프로세스들

### 4.1. yQuant.App.Webhook (Signal Ingress)
- **유형**: ASP.NET Core Minimal API
- **역할**: TradingView Alert를 수신하여 내부 시스템으로 전달
- **메모리 풋프린트**: ~40-60MB (경량 Minimal API)
- **보안**:
    - **IP Whitelist**: 허용된 IP 대역(TradingView 서버 등)에서의 요청만 수락
    - **Secret Key**: Payload 내의 Secret 값 검증
- **로직**:
    - JSON Payload를 `Signal` 객체로 변환 (수량 계산은 하지 않음)
    - Valkey 채널 `signal`로 변환된 Signal 객체 발행(Publish)

### 4.2. yQuant.App.OrderManager (Logic Engine)
- **유형**: Worker Service (Background Process)
- **역할**: Signal을 Order로 변환하고 예약 주문 및 청산을 관리하는 핵심 엔진
- **프로세스**:
    1. **Signal 수신**: Valkey 채널 `signal`을 구독(Subscribe)하여 메시지 수신
    2. **정책 적용**: Signal의 Exchange 정보에 맞는 `ConfigurableMarketRule` 선택 (appsettings.json 기반)
    3. **상태 동기화**: Valkey Cache(`deposit:{Alias}`, `position:{Alias}`)에서 현재 계좌 잔고 및 포지션 조회
    4. **Sizing**: 전략별로 매핑된 `IPositionSizer` 구현체를 통해 자금 관리 규칙 적용 및 수량 계산
    5. **Order 발행**: 유효한 주문 객체를 Valkey 채널 `order`로 발행(Publish)
    6. **예약 주문 관리**: 시간 기반 스케줄에 따라 예약 주문 자동 실행 및 Discord 알림
    7. **청산 관리**: 청산 조건 충족 시 자동 청산 주문 생성
    8. **성과 기록**: 체결 이벤트를 `IPerformanceRepository`에 기록

### 4.3. yQuant.App.BrokerGateway (Execution Gateway)
- **유형**: Worker Service
- **역할**: 증권사와의 물리적 연결 및 상태 동기화 전담
- **주요 기능**:
    - **Order Dispatching**: Valkey 채널 `order`를 구독(Subscribe)하고, 계좌 ID에 맞는 Broker Adapter로 주문 전송
    - **State Synchronization (Hybrid)**: 
        - **Throttled Sync**: 설정된 주기(기본 1분)마다 증권사 API를 호출하여 정확한 잔고 및 포지션 동기화
        - **Local Update**: 주기 내 주문 체결 시, Valkey Cache(`deposit:{Alias}`, `position:{Alias}`)를 즉시 로컬 업데이트(추정치)하여 초고속 반응성 확보
    - **Execution Feedback**: 체결 결과를 Valkey 채널 `execution`으로 발행하여 다른 서비스에 전파
    - **Error Handling**: 주문 실패나 네트워크 오류 발생 시 Discord/Telegram 알림 발송 및 로그 기록
    - **Health Monitoring**: Valkey heartbeat를 통한 서비스 상태 보고

### 4.4. yQuant.App.Console (Manual Control)
- **유형**: Console Application (CLI)
- **역할**: 비상 대응, 테스트, 종목 마스터 데이터 동기화를 위한 통합 도구
- **명령어**:
    - `deposit <currency>`: 예수금 조회
    - `positions <market>`: 포지션 조회
    - `info <ticker>`: 종목 정보 조회
    - `buy <ticker> <qty> [price]`: 매수 주문 (가격 미지정 시 시장가)
    - `sell <ticker> <qty> [price]`: 매도 주문 (가격 미지정 시 시장가)
    - `catalog [country]`: 종목 카탈로그 동기화 (전체 또는 특정 국가)
- **종목 카탈로그 동기화**:
    - **지원 국가**: KR, US, CN, JP, HK, VN
    - **동작**: `yQuant.Infra.Master.KIS`를 통해 증권사 API로부터 거래 가능 종목 리스트를 가져와 Valkey에 캐싱
    - **활용**: systemd timer를 통한 주기적 자동 실행 지원

### 4.5. yQuant.App.Dashboard (Web Dashboard)
- **유형**: Blazor Server Application
- **역할**: 웹 기반 통합 모니터링, 제어 및 리포팅 대시보드
- **주요 기능**:
    - **자산 현황**: Valkey Cache(`account:{Alias}`, `deposit:{Alias}`, `position:{Alias}`)를 조회하여 실시간 예수금, 보유 종목, 평가손익 표시
    - **포지션 관리**: `PositionTable` 공통 컴포넌트를 통해 `Summary`/`Assets` 페이지 간 일관된 정렬(스마트 정렬) 및 총액(Total Amount) 표시
    - **UX 최적화**: 
        - `UiStateService`를 도입하여 페이지 이동 시에도 계좌 선택 및 정렬 상태를 영구적으로 유지
        - 모바일 환경을 고려한 메뉴명 단축("Scheduled") 및 반응형 레이아웃 적용
    - **수동 거래**: 웹 UI를 통한 즉시 매수/매도 주문 실행 (Valkey `order` 채널로 발행)
    - **예약 주문 관리**: 시간 기반 스케줄링(특정 시각, 요일별 반복)을 지원하는 자동 주문 관리 UI
    - **성과 분석 리포팅**:
        - 계좌별 Equity Curve 및 일간 수익률 차트 시각화
        - 주요 성과 지표(Total Return, CAGR, Sharpe Ratio, Max Drawdown 등) 자동 계산 및 표시
        - CSV 데이터 익스포트 (QuantStats 호환)
    - **서비스 상태 모니터링**: 각 애플리케이션의 health status 실시간 표시 (DI 스코프 이슈 해결로 안정성 확보)
    - **실시간 알림**: `RealtimeEventService`를 통해 주문 체결 등 주요 이벤트를 즉시 사용자에게 알림 (Snackbar)

### 4.6. yQuant.App.Notifier (Notification Dispatcher)
- **유형**: Worker Service (Background Process)
- **역할**: Valkey 이벤트를 구독하여 Discord 및 Telegram으로 알림을 전파하는 중앙 집중식 알림 서비스
- **아키텍처**: `yQuant.Infra.Notification` 라이브러리를 기반으로 구축되어 다른 애플리케이션과 유기적으로 연동
- **주요 기능**:
    - **Multi-Channel Subscription**: Valkey Pub/Sub 채널 구독
        - `notifications:orders` - 주문 요청/체결/실패 알림
        - `notifications:schedules` - 예약 주문 관리 알림
        - `notifications:positions` - 포지션 변경 알림
        - `notifications:system` - 시스템 상태 및 이벤트 알림
    - **Intelligent Routing**: 
        - 계좌별로 다른 Discord Webhook URL로 메시지 분기
        - 메시지 타입에 따른 Telegram 필터링 지원
    - **Performance Optimization**:
        - 배치 처리를 통한 효율적인 메시지 전송
        - Rate limiting으로 Discord API 제한 준수
        - 비동기 처리로 메인 시스템 영향 최소화
    - **Reliability**:
        - 재시도 로직으로 일시적 네트워크 오류 대응
        - 큐 기반 메시지 버퍼링으로 메시지 손실 방지
- **설정 기반 제어**: `appsettings.json`에서 메시지 라우팅, 타임아웃, 재시도 정책 등을 동적으로 구성


