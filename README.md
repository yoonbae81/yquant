# **yQuant: 자동매매 및 리스크 관리 시스템**

## **1\. 개요**

* **시스템명**: yQuant  
* **목적**: TradingView 신호와 증권사 API(한국투자증권 등)를 연동하여 한국(KRX) 및 미국(NASDAQ, AMEX 등) 주식을 거래하는 자동매매 시스템 구축 (가상화폐 확장 고려)  
* **아키텍처 원칙**: 헥사고날 아키텍처(Hexagonal Architecture) 적용. 도메인 표준(Core), 인프라 구현(Infra), 매매 정책(Policy), 실행 환경(App)의 4계층 분리  
* **핵심 통신 방식**: Redis Pub/Sub을 이용한 비동기 메시징 및 이벤트 기반(Event-Driven) 처리

## **2\. 주요 기능 (System Features)**

시스템이 제공하는 핵심 기능을 트레이딩, 운영, 모니터링, 인프라 분야로 구분하여 기술함

### **2.1. 트레이딩 자동화 (Trading Automation)**

* **신호 수신**: TradingView Webhook을 통한 실시간 매매 신호 수신 및 **거래소 정보(Exchange)** 표준 객체 변환  
* **멀티 마켓 지원**: 한국(KRX) 및 미국(NYSE/AMEX/NASDAQ) 시장의 개장 시간, 통화, 거래 규칙을 동시에 로드하여 24시간 자동 대응  
* **포지션 사이징**: 계좌 잔고 및 자금 관리 규칙(Policy Plugin)에 기반한 최적 주문 수량 자동 산출 (Sizing)  
* **초저지연 집행**: Redis Pub/Sub 기반의 비동기 메시징을 통한 고속 주문 집행

### **2.2. 매매 제어 및 운영 (Control & Operation)**

* **수동 개입**: 대시보드를 통한 종목별 즉시 추가 매수/매도 실행  
* **콘솔 도구**: 터미널 환경에서의 긴급 주문 실행 및 시스템 테스트 지원  
* **예약 주문**: 정해진 시간에 시장가 매수/매도 주문 자동 실행 (금액 입력 시 예상 수량 자동 계산 지원)

### **2.3. 모니터링 및 시각화 (Monitoring)**

* **자산 현황**: 실시간 예수금, 총 매입 금액, 추정 자산 조회 (KRW/USD 통합 가치 환산)  
* **포트폴리오 관리**: Redis에 캐싱된 보유 종목 데이터 기반의 평가손익(PnL), 수익률(ROI) 실시간 출력  
* **성과 분석 데이터**: QuantStats 등 외부 분석 도구 호환을 위한 일간 수익률 및 자산 변동 로그(CSV) 자동 생성  
* **실시간 알림**: 매매 체결 및 시스템 중요 이벤트 발생 시 텔레그램(Telegram)을 통한 즉각적인 모바일 통지  
* **데이터 흐름 추적**: Signal 수신부터 Order 집행까지의 프로세스 모니터링

### **2.4. 인프라 및 확장성 (Infrastructure)**

* **멀티 브로커**: 한국투자증권 등 다중 증권사 어댑터 지원 및 라우팅  
* **인증 캡슐화**: 증권사별 상이한 인증 방식(로그인창 제어, 토큰 수명주기 관리)을 내부적으로 은닉하여 처리  
* **플러그인 아키텍처**: 매매 전략 및 리스크 관리 로직을 DLL 플러그인 형태로 분리하여 무중단/독립 배포 지원

## **3\. 시스템 아키텍처 설계**

### **3.1. 계층 구조 (Layered Architecture)**

* **Core Layer (Domain)**: 시스템의 불변 법칙(Invariants), 데이터 표준(Model), 포트(Port) 정의  
* **Infrastructure Layer (Adapter)**: Core 포트의 기술적 구현체(Redis 통신, 증권사 API 래핑, 알림 서비스 등)  
* **Policy Layer (Plugin)**: 가변적인 자금 관리(Sizing), 시장 규칙(Market) 등 정책 구현체. **복수의 시장 정책 동시 로드 및 N:1 맵핑 지원**  
* **Application Layer (Host)**: 위 계층들을 조립(Composition)하여 실제 메모리 상에서 구동되는 실행 프로세스

### **3.2. 기술 스택**

* **Language**: C\# 14 / .NET 10.0  
* **Message Broker & Cache**: Redis  
* **Web Framework**: ASP.NET Core (Minimal API, Blazor Server)  
* **IDE**: Visual Studio 2026

## **4\. Visual Studio 솔루션 구성 (yQuant.sln)**

### **4.1. 솔루션 구조도**

yQuant.Solution  
│  
├── 📂 01.Core (Solution Folder)  
│   └── 📄 yQuant.Core.csproj (Class Library)  
│       ├── 📂 Models (Domain Entities, VOs)  
│       ├── 📂 Ports  
│       │   ├── 📂 Input (Primary Ports: Use Cases)  
│       │   └── 📂 Output (Secondary Ports)  
│       │       ├── 📂 Infrastructure (Infra Interfaces)  
│       │       └── 📂 Policies (Policy Interfaces)  
│       └── 📂 Services (Input Port Implementations)  
│  
├── 📂 02.Infrastructure (Solution Folder)  
│   ├── 📄 yQuant.Infra.Middleware.Redis.csproj (Class Library)  
│   ├── 📄 yQuant.Infra.Broker.KIS.csproj (Class Library)  
│   ├── 📄 yQuant.Infra.Notification.Telegram.csproj (Class Library)  
│   └── 📄 yQuant.Infra.Reporting.QuantStats.csproj (Class Library)  
│  
├── 📂 03.Applications (Solution Folder)  
│   ├── 📄 yQuant.App.BrokerGateway.csproj (Worker Service)  
│   ├── 📄 yQuant.App.OrderComposer.csproj (Worker Service)  
│   ├── 📄 yQuant.App.TradingViewWebhook.csproj (ASP.NET Core Minimal API)  
│   ├── 📄 yQuant.App.Console.csproj (Console App)  
│   └── 📄 yQuant.App.Web.csproj (Blazor Server App)  
│  
└── 📂 04.Policies (Solution Folder)  
    ├── 📄 yQuant.Policies.Sizing.Basic.csproj (Class Library)  
    ├── 📄 yQuant.Policies.Market.Korea.csproj (Class Library)  
    └── 📄 yQuant.Policies.Market.UnitedStates.csproj (Class Library)

### **4.2. 프로젝트별 상세 역할**

#### **A. 📂 01.Core (The Law)**

* **yQuant.Core**  
  * **역할**: 시스템의 골격 및 공용 언어(Ubiquitous Language) 정의  
  * **주요 내용**:  
    * **Models**:  
      * **Signal**: Symbol, **Exchange (e.g., KRX, NASDAQ)**, EntryPrice, Action 등  
      * **Order**: Signal 정보 기반으로 생성된 최종 주문 객체  
    * **Ports**: 외부와의 소통을 위한 인터페이스 집합 (Input/Output)  
    * **Services**: Input Port(UseCase) 인터페이스를 구현한 순수 비즈니스 로직 클래스 집합  
  * **1\. Input Ports (Primary Ports \- Use Cases)**  
    * **역할**: 외부(UI, App)에서 도메인 로직을 실행하기 위해 호출하는 인터페이스  
    * **IOrderCompositionUseCase**: 외부 신호 기반 주문 조립 흐름 제어  
    * **IPositionLiquidationUseCase**: 긴급 청산 및 일괄 매도 처리  
    * **IAssetEvaluationUseCase**: 자산 가치 평가  
    * **IManualTradingUseCase**: 사용자 수동 주문 처리  
  * **2\. Output Ports (Secondary Ports)**  
    * **역할**: 도메인 로직이 외부 기술(Infra)이나 로직(Policy)을 사용하기 위해 정의한 인터페이스  
    * **Infrastructure**:  
      * **IBrokerConnector**: 증권사 통신 규약 (접속, 주문, 잔고 조회)  
      * **INotificationService**: 알림 발송 규약  
      * **IPerformanceExporter**: 성과 리포팅 규약  
    * **Policies**:  
      * **IPositionSizer**: 자금 관리 정책 규약 (수량 계산)  
      * **IMarketRule**: 시장별 운영 규칙 규약. **CanHandle(string exchange)** 메서드를 통해 N개의 거래소에 대한 지원 여부를 판단 (N:1 Mapping)  
  * **3\. Services (Input Port Implementations)**  
    * **역할**: Input Port 인터페이스를 구현하여 실제 비즈니스 흐름을 제어하는 어플리케이션 서비스 (Application Service)  
    * **OrderCompositionService** (IOrderCompositionUseCase 구현):  
      * **다중 시장 지원**: 주입된 **여러 IMarketRule 중 Signal.Exchange를 처리 가능한(CanHandle \== true) Rule을 선택**하여 적용  
      * **주문 조립 파이프라인**: Signal 수신 \-\> **SelectedMarketRule로 개장 여부 확인** \-\> IPositionSizer로 수량(Size) 계산 \-\> Order 객체 생성 \-\> 유효성 검증 \-\> IBrokerConnector로 전송 요청  
    * **PositionLiquidationService** (IPositionLiquidationUseCase 구현):  
      * **일괄 청산**: 보유 전 종목 조회 \-\> 종목별 해당 MarketRule 적용 \-\> 매도 주문 일괄 조립 \-\> 병렬 전송  
    * **AssetEvaluationService** (IAssetEvaluationUseCase 구현):  
      * **자산 가치 평가**: 모든 MarketRule을 순회하며 통화별(KRW/USD) 자산 평가 후 기준 통화로 합산  
    * **ManualTradingService** (IManualTradingUseCase 구현):  
      * **수동 주문 집행**: 사용자 입력값 검증 \-\> (옵션) IPositionSizer 검증 \-\> 즉시 전송

#### **B. 📂 02.Infrastructure (The Tools)**

* **yQuant.Infra.Middleware.Redis**: Redis Pub/Sub 메시징 및 상태 캐싱 구현  
* **yQuant.Infra.Broker.KIS**: 한국투자증권 REST API 구현체 (IBrokerConnector 구현) \- 한국/미국 주식 API 엔드포인트 통합 처리  
* **yQuant.Infra.Notification.Telegram**: Telegram Bot API 구현체 (INotificationService 구현)  
* **yQuant.Infra.Reporting.QuantStats**: CSV 파일 리포팅 구현체 (IPerformanceExporter 구현)

#### **C. 📂 03.Applications (The Runners)**

* **yQuant.App.BrokerGateway** (Gateway)  
  * **역할**: 증권사 통신 통합 게이트웨이 (물리적 연결 담당)  
  * **동작**:  
    * **Outbound**: Redis Order 수신 \-\> 어댑터(KIS)로 주문 실행  
    * **Inbound**: 체결 통보 수신 및 주기적 데이터 Polling \-\> Redis 캐시 동기화  
  * **특징**: 증권사 연결 수명주기 관리, 인증 로직 은닉  
* **yQuant.App.OrderComposer** (Composer)  
  * **역할**: 신호(Signal)를 받아 실행 가능한 주문(Order)으로 조립하는 작성기  
  * **동작**: Redis Signal 수신 \-\> **OrderCompositionService** 호출 \-\> (내부적으로 **CanHandle로 매칭된 MarketRule** 및 PositionSizer 사용) \-\> 완성된 Order를 Redis 발행  
  * **설정**: appsettings.json에서 **로드할 Market Policy 플러그인 목록(Array)** 지정  
* **yQuant.App.TradingViewWebhook** (Webhook)  
  * **역할**: TradingView Webhook 수신 및 Signal 변환  
  * **특징**: 페이로드의 exchange 값을 **Signal.Exchange 필드에 그대로 매핑** (로직 없음)  
* **yQuant.App.Console** (Manual Tool)  
  * **역할**: 수동 주문 실행 및 테스트 도구  
* **yQuant.App.Dashboard** (Integrated UI)  
  * **역할**: 모니터링 및 제어

#### **D. 📂 04.Policies (The Logic)**

* **yQuant.Policies.Sizing.Basic**  
  * **역할**: **IPositionSizer** (Output Port) 구현체  
  * **내용**: Signal과 Account 정보를 입력받아 구체적인 매수 수량을 계산하는 알고리즘  
* **yQuant.Policies.Market.Korea**  
  * **역할**: **IMarketRule** (Output Port) 구현체 \- 한국 시장용  
  * **처리 대상(Mapping)**: **KRX, KOSPI, KOSDAQ**  
  * **내용**: 통화(KRW), 개장 시간(09:00\~15:30) 로직  
* **yQuant.Policies.Market.UnitedStates**  
  * **역할**: **IMarketRule** (Output Port) 구현체 \- 미국 시장용  
  * **처리 대상(Mapping)**: **NASDAQ, NYSE, AMEX**  
  * **내용**: 통화(USD), 개장 시간(23:30\~06:00, 썸머타임 적용), 프리마켓 허용 여부

## **5\. 런타임 프로세스 및 데이터 흐름**

### **5.1. 상시 실행 프로세스**

1. **TradingViewWebhook**: \[외부\] \-\> (HTTP) \-\> \[Redis Signal (Exchange="NASDAQ")\]  
2. **OrderComposer**: \[Redis Signal\] \-\> (Select US Policy via CanHandle("NASDAQ")) \-\> \[Redis Order\]  
3. **BrokerGateway**: \[Redis Order\] \-\> (API Adapter) \-\> \[증권사 API\]  
4. **Dashboard**: \[User\] \-\> (UI) \-\> \[Redis Order\]

### **5.2. 데이터 파이프라인**

* **Signal Flow**: TradingView \-\> Webhook \-\> **Redis (Signal)** \-\> OrderComposer (Routes to KR/US Policy) \-\> **Redis (Order)** \-\> BrokerGateway \-\> Broker  
* **Manual Flow**: User \-\> Console/Dashboard \-\> **Redis (Order)** \-\> BrokerGateway \-\> Broker
