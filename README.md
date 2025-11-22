# **yQuant: 자동매매 및 리스크 관리 시스템**

## **1\. 개요**

* **시스템명**: yQuant  
* **목적**: TradingView 신호와 증권사 API(한국투자증권 등)를 연동하여 한국(KRX) 및 미국(NASDAQ, AMEX 등) 주식을 거래하는 자동매매 시스템 구축 (가상화폐 확장 고려)  
* **아키텍처 원칙**: 헥사고날 아키텍처(Hexagonal Architecture) 적용. 도메인 표준(Core), 인프라 구현(Infra), 매매 정책(Policy), 실행 환경(App)의 4계층 분리  
* **핵심 통신 방식**: Redis Pub/Sub을 이용한 비동기 메시징 및 이벤트 기반(Event-Driven) 처리

## **2\. 주요 기능 (System Features)**

시스템이 제공하는 핵심 기능을 트레이딩, 운영, 모니터링, 인프라 분야로 구분하여 기술함

### **2.1. 트레이딩 자동화 (Trading Automation)**

* **신호 수신**: TradingView Webhook을 통한 실시간 매매 신호 수신 및 표준 객체 변환  
* **포지션 사이징**: 계좌 잔고 및 리스크 관리 규칙(Rule Plugin)에 기반한 최적 주문 수량 자동 산출  
* **초저지연 집행**: Redis Pub/Sub 기반의 비동기 메시징을 통한 고속 주문 집행

### **2.2. 매매 제어 및 운영 (Control & Operation)**

* **수동 개입**: 대시보드를 통한 종목별 즉시 추가 매수/매도 실행  
* **콘솔 도구**: 터미널 환경에서의 긴급 주문 실행 및 시스템 테스트 지원  
* **예약 주문**: 정해진 시간에 시장가 매수/매도 주문 자동 실행 (금액 입력 시 예상 수량 자동 계산 지원)

### **2.3. 모니터링 및 시각화 (Monitoring)**

* **자산 현황**: 실시간 예수금, 총 매입 금액, 추정 자산 조회  
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
* **Policy Layer (Plugin)**: 가변적인 매매 정책 및 자금 관리 로직(Risk Management) 구현체. 플러그인 방식 교체  
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
│       │   └── 📂 Output (Secondary Ports: Infra Interfaces)  
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
│   ├── 📄 yQuant.App.PositionManager.csproj (Worker Service)  
│   ├── 📄 yQuant.App.TradingViewWebhook.csproj (ASP.NET Core Minimal API)  
│   ├── 📄 yQuant.App.Console.csproj (Console App)  
│   └── 📄 yQuant.App.Web.csproj (Blazor Server App)  
│  
└── 📂 04.Policies (Solution Folder)  
    └── 📄 yQuant.Policies.Sizing.Basic.csproj (Class Library)

### **4.2. 프로젝트별 상세 역할**

#### **A. 📂 01.Core (The Law)**

* **yQuant.Core**  
  * **역할**: 시스템의 골격 및 공용 언어(Ubiquitous Language) 정의  
  * **주요 내용**:  
    * **Models**: Order, Signal, AccountInfo, PerformanceLog, Position 등 표준 데이터 모델  
    * **Ports**: 외부와의 소통을 위한 인터페이스 집합 (Input/Output)  
    * **Services**: Input Port(UseCase) 인터페이스를 구현한 순수 비즈니스 로직 클래스 집합  
  * **1\. Input Ports (Primary Ports \- Use Cases)**  
    * **역할**: 외부(UI, App)에서 도메인 로직을 실행하기 위해 호출하는 인터페이스  
    * **IAssetEvaluationUseCase**: 자산 가치 및 주문 수량 계산  
    * **IOrderProcessingUseCase**: 외부 신호 기반 주문 처리 흐름 제어  
    * **IManualTradingUseCase**: 사용자 수동 주문 처리  
    * **IPortfolioManagementUseCase**: 포트폴리오 일괄 청산 등 관리 기능  
  * **2\. Output Ports (Secondary Ports \- Infrastructure Interfaces)**  
    * **역할**: 도메인 로직이 외부 기술(DB, API 등)을 사용하기 위해 정의한 인터페이스  
    * **IBrokerConnector**: 증권사 통신 규약 (접속, 주문, 잔고 조회)  
    * **IRiskManager**: 리스크 관리 정책 규약 (수량 계산, 검증)  
    * **INotificationService**: 알림 발송 규약  
    * **IPerformanceExporter**: 성과 리포팅 규약  
  * **3\. Services (Input Port Implementations)**  
    * **역할**: Input Port 인터페이스를 구현하여 실제 비즈니스 흐름을 제어하는 어플리케이션 서비스 (Application Service)  
    * **AssetEvaluationService** (IAssetEvaluationUseCase 구현):  
      * **자산 가치 평가**: IBrokerConnector를 통해 잔고 조회 후 통화 변환 및 총액 합산  
      * **수량 산출**: 지정 금액을 현재가로 나누어 매수 가능 수량 계산 (호가 단위 고려)  
    * **OrderProcessingService** (IOrderProcessingUseCase 구현):  
      * **신호 처리 파이프라인**: Signal 수신 \-\> IRiskManager로 수량 계산 \-\> Order 객체 생성 \-\> 유효성 검증 \-\> IBrokerConnector로 주문 전송  
    * **ManualTradingService** (IManualTradingUseCase 구현):  
      * **수동 주문 집행**: 사용자 입력값 검증 \-\> IRiskManager 검증(옵션) \-\> IBrokerConnector로 즉시 전송  
    * **PortfolioManagementService** (IPortfolioManagementUseCase 구현):  
      * **긴급 청산**: 보유 전 종목 조회(GetPositionsAsync) \-\> 종목별 시장가 매도 주문 일괄 생성 \-\> 병렬 전송 처리  
  * **특징**: 비즈니스 로직 중 '변하지 않는 규칙(Invariants)'만 포함하며 시스템 표준 변경 최소화 원칙을 준수함

#### **B. 📂 02.Infrastructure (The Tools)**

* **yQuant.Infra.Middleware.Redis**: Redis Pub/Sub 메시징 및 상태 캐싱 구현  
* **yQuant.Infra.Broker.KIS**: 한국투자증권 REST API에 직접 접속하여 인증(토큰), 주문 요청 로직을 수행하는 구현체 (IBrokerConnector 구현)  
* **yQuant.Infra.Notification.Telegram**: Telegram Bot API를 활용하여 INotificationService 구현. 메시지 포맷팅 및 발송 로직 담당  
* **yQuant.Infra.Reporting.QuantStats**: IPerformanceExporter 구현체. 일간 자산 및 수익률 데이터를 QuantStats 호환 CSV 포맷(Date, Equity, Return)으로 변환하여 저장

#### **C. 📂 03.Applications (The Runners)**

* **yQuant.App.BrokerGateway** (Gateway)  
  * **역할**: 증권사 통신 통합 게이트웨이, 알림 및 리포팅 트리거  
  * **동작**:  
    * **Outbound**: Redis Order 수신 \-\> 어댑터(KIS)로 주문 실행  
    * **Inbound**: 체결 통보 수신 및 주기적 잔고/보유종목 조회(Polling) \-\> Redis 캐시 동기화  
    * **Reporting**: 일 마감(EOD) 시점 자산 스냅샷 생성 및 **성과 로그(CSV) 저장 요청**  
    * **Notification**: 체결 및 주요 이벤트 발생 시 **텔레그램 알림 발송 요청**  
  * **주요 설정 (appsettings.json)**:  
    * ActiveBroker: 활성화할 증권사 어댑터 식별자 (예: "KIS")  
    * Authentication: 증권사 접속 인증 정보  
    * TelegramSettings: Bot Token 및 Target Chat ID  
    * Reporting: CSV 저장 경로 및 활성화 여부  
  * **특징**: 증권사 연결 수명주기(Lifecycle) 관리, 인증 로직 은닉(추상 메서드 호출)  
* **yQuant.App.PositionManager** (Manager)  
  * **역할**: 포지션 관리 및 매매 정책 실행 호스트  
  * **동작**: Redis Signal 수신 \-\> 정책 플러그인에 잔고 기반 수량 계산(Sizing) 요청 \-\> Order 생성 및 Redis 발행  
  * **주요 설정 (appsettings.json)**:  
    * ActivePolicy: 로드할 정책 플러그인 DLL 경로 및 클래스명  
    * RiskParameters: 정책 알고리즘에 전달할 리스크 변수 (예: 1회 거래당 최대 손실 허용률, 기본 레버리지 비율)  
  * **핵심 가치**: 신호(Intent)를 실제 주문 가능한 수량(Quantity)으로 구체화  
* **yQuant.App.TradingViewWebhook** (Webhook)  
  * **역할**: TradingView Webhook 수신 전용 엔드포인트  
  * **동작**: HTTP Request 수신 \-\> Payload 검증 \-\> Signal 변환 \-\> Redis 발행  
  * **특징**: Minimal API 적용, 로직 최소화  
* **yQuant.App.Console** (Manual Tool)  
  * **역할**: 수동 주문 실행 및 테스트 도구  
  * **동작**: 사용자 입력 파싱 \-\> 유효성 검증 \-\> Redis Order 채널 직접 발행  
* **yQuant.App.Dashboard** (Integrated UI)  
  * **역할**: 시스템 모니터링, 수동 개입, 예약 주문 관리  
  * **동작**:  
    * Redis 캐시(Account, Position) 기반 보유종목 및 자산 현황 출력  
    * 예약 주문 스케줄러: 설정된 시간에 시장가 주문 발행 (금액 입력 기반 수량 자동 계산)  
    * **자산 조회**: IAssetEvaluationUseCase를 통해 계산된 자산 가치 시각화

#### **D. 📂 04.Policies (The Logic)**

* **yQuant.Policies.Sizing.Basic**  
  * **역할**: IRiskManager (Output Port) 구현체  
  * **내용**: Signal과 Account 정보를 입력받아 구체적인 매수 수량을 계산하는 알고리즘(가변 정책)  
  * **특징**: Core 포트에 의존하며, 변경 시 해당 DLL만 교체 배포 가능

## **5\. 런타임 프로세스 및 데이터 흐름**

### **5.1. 상시 실행 프로세스 (3 Daemons \+ 1 Web App)**

시스템 가동을 위해 반드시 실행되어야 하는 독립 프로세스

1. **TradingViewWebhook**: \[외부\] \-\> (HTTP) \-\> \[Redis Signal\]  
2. **PositionManager**: \[Redis Signal\] \-\> (Policy Logic) \-\> \[Redis Order\]  
3. **BrokerGateway**: \[Redis Order\] \-\> (Adapter) \-\> \[증권사 API\] \-\> \[Telegram/CSV\]  
4. **Dashboard**: \[User/Schedule\] \-\> (UI/BG) \-\> \[Redis Order\]

### **5.2. 데이터 파이프라인**

* **Signal Flow**: TradingView \-\> TradingViewWebhook \-\> **Redis (Signal Ch)** \-\> PositionManager (with Plugin) \-\> **Redis (Order Ch)** \-\> BrokerGateway \-\> Broker  
* **Manual/Scheduled Flow**: User / Scheduler \-\> Console / Dashboard \-\> **Redis (Order Ch)** \-\> BrokerGateway \-\> Broker  
* **Account Flow**: Broker \-\> BrokerGateway \-\> **Redis (Cache)** \<- PositionManager / Dashboard (Read)  
* **Notification & Reporting Flow**: Broker (체결/마감) \-\> BrokerGateway \-\> Telegram API (알림) / File System (CSV 리포트)
