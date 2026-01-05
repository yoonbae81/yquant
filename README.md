# **yQuant: 자동매매 및 리스크 관리 시스템**

## **1. 개요**

* **시스템명**: yQuant
* **목적**: TradingView 신호와 증권사 API(한국투자증권 등)를 연동하여 한국(KRX) 및 미국(NASDAQ, AMEX 등) 주식을 거래하는 자동매매 시스템 구축 (가상화폐 확장 고려)
* **아키텍처 원칙**: 헥사고날 아키텍처(Hexagonal Architecture) 적용. 도메인 표준(Core), 인프라 구현(Infra), 매매 정책(Policy), 실행 환경(App)의 4계층 분리
* **핵심 통신 방식**: Valkey Pub/Sub을 이용한 비동기 메시징 및 이벤트 기반(Event-Driven) 처리

## **2. 주요 기능 (System Features)**

시스템 핵심 기능을 트레이딩, 운영, 모니터링, 인프라 분야로 구분하여 기술

### **2.1. 트레이딩 자동화 (Trading Automation)**

* **신호 수신**: TradingView Webhook을 통한 실시간 매매 신호 수신 및 **거래소 정보(Exchange)** 표준 객체 변환
* **멀티 마켓 지원**: 한국(KRX) 및 미국(NYSE/AMEX/NASDAQ) 시장의 개장 시간, 통화, 거래 규칙을 동시에 로드하여 24시간 자동 대응
* **포지션 사이징**: 계좌 잔고 및 자금 관리 규칙(Policy Plugin)에 기반한 최적 주문 수량 자동 산출 (Sizing)
* **초저지연 집행**: Valkey Pub/Sub 기반의 비동기 메시징을 통한 고속 주문 집행

### **2.2. 매매 제어 및 운영 (Control & Operation)**

* **수동 개입**: 대시보드를 통한 종목별 즉시 추가 매수/매도 실행
* **콘솔 도구**: 터미널 환경에서의 긴급 주문 실행 및 시스템 테스트 지원
* **예약 주문**: 정해진 시간에 시장가 매수/매도 주문 자동 실행 (금액 입력 시 예상 수량 자동 계산 지원)

### **2.3. 모니터링 및 시각화 (Monitoring & Reporting)**

* **자산 현황**: 실시간 예수금, 총 매입 금액, 추정 자산 조회 (KRW/USD 통합 가치 환산)
* **포트폴리오 관리**: Valkey에 캐싱된 보유 종목 데이터 기반의 평가손익(PnL), 수익률(ROI) 실시간 출력
* **성과 분석 대시보드**: 
    - 계좌별 누적 수익률(Equity Curve) 및 일간 수익률 시각화
    - 주요 성과 지표(CAGR, Sharpe Ratio, MDD 등) 자동 계산 및 표시
    - 성과 데이터 CSV/JSON 익스포트 지원 (QuantStats 호환)
* **실시간 알림**: 매매 체결 및 시스템 중요 이벤트 발생 시 디스코드(Discord) 및 텔레그램(Telegram)을 통한 즉각적인 모바일 통지
* **데이터 흐름 추적**: Signal 수신부터 Order 집행까지의 프로세스 모니터링

### **2.4. 인프라 및 확장성 (Infrastructure)**

* **멀티 브로커**: 한국투자증권 등 다중 증권사 어댑터 지원 및 라우팅
* **인증 캡슐화**: 증권사별 상이한 인증 방식(로그인창 제어, 토큰 수명주기 관리)을 내부적으로 은닉하여 처리
* **플러그인 아키텍처**: 매매 전략 및 리스크 관리 로직을 DLL 플러그인 형태로 분리하여 무중단/독립 배포 지원

## **3. 시스템 아키텍처 설계**

### **3.1. 계층 구조 (Layered Architecture)**

* **Core Layer (Domain)**: 시스템의 불변 법칙(Invariants), 데이터 표준(Model), 포트(Port) 정의
* **Infrastructure Layer (Adapter)**: Core 포트의 기술적 구현체(Valkey 통신, 증권사 API 래핑, 알림 서비스 등)
* **Policy Layer (Plugins)**: 포지션 사이징 전략 플러그인
* **Application Layer (Runners)**: 실행 가능한 프로세스들 (BrokerGateway, Web, Webhook, OrderManager 등)

### **3.2. 주요 모듈 구성**

#### **Core Layer** (`/src/01.Core`)
*   `yQuant.Core`: 도메인 모델, 포트(Input/Output), 핵심 서비스

#### Infrastructure Layer (`/src/02.Infrastructure`)
*   `yQuant.Infra.Broker.KIS`: 한국투자증권 API 어댑터
*   `yQuant.Infra.Valkey`: Valkey Pub/Sub 메시징, 캐싱 및 연결 관리
*   `yQuant.Infra.Notification`: 공통 알림 모델 및 메시지 발행 로직
*   `yQuant.Infra.Notification.Discord`: Discord 알림 서비스 구현
*   `yQuant.Infra.Notification.Telegram`: Telegram 알림 서비스 구현
*   `yQuant.Infra.Reporting`: 계좌 성과 기록 및 파일 기반 데이터 저장소

#### Policy Layer (`/src/04.Policies`)
*   `yQuant.Policies.Sizing`: 자금 관리 및 포지션 사이징 전략 구현체

#### Application Layer (`/src/03.Applications`)
*   `yQuant.App.Webhook`: TradingView 신호 수신 웹서버 (Minimal API)
*   `yQuant.App.OrderManager`: 신호-주문 변환, 스케줄 엔진 및 자금 관리
*   `yQuant.App.BrokerGateway`: 증권사 연결 게이트웨이 및 상태 동기화
*   `yQuant.App.Dashboard`: 웹 기반 통합 대시보드 (Blazor Server)
*   `yQuant.App.Console`: CLI 기반 관리 도구 및 마스터 데이터 동기화
*   `yQuant.App.Notifier`: Valkey 이벤트를 구독하여 멀티 채널 알림 배포

## **4. High Availability (HA) & Deployment Strategy**

yQuant는 무중단 운영과 데이터 안정성을 위해 **Blue-Green** 배포 전략과 **Valkey Sentinel** 기반의 2중화 구조를 채택하고 있습니다. (이는 상시 가동이 필요한 프로덕션 환경을 위한 권장 옵션임을 분명히 합니다.)

### **4.1. 서버 구성 (3-VM 구조)**

*   **`yq-port`** (E2.Micro): 시스템의 관문. HAProxy를 통해 트래픽을 분산하며, 증권사 토큰 및 공통 데이터 저장소인 Storage Valkey와 Sentinel 중재자 역할을 수행합니다.
*   **`yq-blue` / `yq-green` (A1.Flex)**: 실제 연산 및 웹 대시보드가 구동되는 쌍둥이 노드입니다. 메시징용 Valkey 마스터/슬레이브 복제 구성을 가집니다.

### **4.2. Blue-Green 배포 워크플로우**

1.  **Staging 배포**: 현재 비활성(Standby) 상태인 노드(예: `green`)에 새로운 코드를 배포합니다.
2.  **검증**: `yq-port`의 스테이징 포트를 통해 신규 버전의 동작을 확인합니다.
3.  **역할 교체 (Promotion)**: 이상이 없으면 HAProxy 설정을 변경하여 `green`을 Active로 전환하고, Valkey 마스터를 승격시킵니다.
4.  **안정화**: 기존 Active였던 `blue`는 차기 배포를 위한 Standby 상태가 됩니다.

상세한 하드웨어 구성 및 실전 배포 방법은 [HA 아키텍처 가이드](/docs/ha-strategy.md)를 참조하세요.

## **5. 설치 및 설정 (Installation & Configuration)**

### **4.1. 요구사항 (Prerequisites)**
* 한국투자증권 Open API 계정 (AppKey, AppSecret)

### **4.2. 프로젝트 클론 및 빌드**
```bash
git clone https://github.com/yoonbae81/yQuant.NET.git
cd yQuant.NET
dotnet build yQuant.slnx
```

### **4.3. 설정 (Configuration)**
솔루션 루트의 JSON 파일들을 통해 중앙 집중식으로 설정을 관리합니다. `example` 파일을 복사하여 실제 설정 파일을 생성하세요.

**설정 파일 생성:**
```bash
# 기본 설정 파일 생성
cp appsettings.example.json appsettings.json

# 민감한 정보가 포함된 secrets 파일 생성
cp appsecrets.example.json appsecrets.json

# appsecrets.json 파일에 Valkey 접속 정보 설정
# {
#   "Valkey": {
#     "Message": "localhost:6379",
#     "Storage": "your-shared-valkey-url"
#   },
#   ...
# }
```

**주요 설정:**
*   **appsettings.json**: 애플리케이션 공통 설정, 시장 규칙(MarketRules), 카탈로그 URL, 포지션 사이징 정책(StrategySizingMapping)
*   **appsecrets.json**: 민감한 정보 (Valkey 접속 정보, KIS API 키, Discord/Telegram 토큰, 웹 사용자 계정)

상세한 하드웨어 구성 및 실전 배포 방법은 [HA 아키텍처 가이드](/docs/ha-strategy.md)를 참조하세요.

> **보안 주의**: `appsecrets.json` 등 민감한 정보가 포함된 파일은 `.gitignore`에 의해 버전 관리에서 제외됩니다.

## **6. 실행 방법 (Execution Instructions)**

시스템은 여러 개의 독립적인 프로세스로 구성되며, `scripts` 폴더의 통합 스크립트를 통해 관리됩니다.

### **6.1. 통합 관리 (권장)**
시스템 전체를 한 번에 설정하고 실행하려면 다음 스크립트를 사용합니다.

```bash
# 1. 시스템 환경 설정 (최초 1회)
bash scripts/setup.sh

# 2. 전체 빌드 및 배포
bash scripts/deploy.sh

# 3. 서비스 상태 통합 점검
bash scripts/health-check.sh
```

### **6.2. 개별 프로세스 실행 (개발용)**
증권사 API와 통신을 담당하는 게이트웨이. 가장 먼저 실행 필요
```bash
cd src/03.Applications/yQuant.App.BrokerGateway
dotnet run
```

### **5.2. Webhook 서버 실행**
TradingView 신호를 수신하는 웹 서버
```bash
cd src/03.Applications/yQuant.App.Webhook
dotnet run
```

### **5.3. Order Manager 실행**
신호를 주문으로 변환하고 예약 주문을 관리하는 로직 처리기
```bash
cd src/03.Applications/yQuant.App.OrderManager
dotnet run
```

### **5.4. Web Dashboard 실행**
웹 기반 모니터링 및 제어 대시보드
```bash
cd src/03.Applications/yQuant.App.Dashboard
dotnet run
```
브라우저에서 `http://localhost:5000` 접속

### **5.5. Console 도구 사용**
수동 주문, 시스템 상태 확인 및 종목 마스터 데이터 동기화를 위한 통합 CLI 도구

#### 기본 사용법
```bash
cd src/03.Applications/yQuant.App.Console
dotnet run -- <command> [account] [args]
```

#### 사용 가능한 명령어
```bash
# 예수금 조회
dotnet run -- deposit MainAccount KRW

# 포지션 조회
dotnet run -- positions MainAccount KR

# 종목 정보 조회 (계정 불필요)
dotnet run -- info 005930

# 매수 주문
dotnet run -- buy MainAccount 005930 10

# 매도 주문
dotnet run -- sell MainAccount 005930 10

# 종목 마스터 데이터 동기화 (계정 불필요)
dotnet run -- catalog
dotnet run -- catalog KR
dotnet run -- catalog US
```

### **5.6. 종목 마스터 데이터 동기화**
종목 마스터 데이터 동기화는 Console 애플리케이션의 `catalog` 명령어를 사용합니다. (계정 정보가 필요하지 않습니다)

```bash
cd src/03.Applications/yQuant.App.Console

# 모든 국가 동기화
dotnet run -- catalog

# 특정 국가만 동기화
dotnet run -- catalog KR
dotnet run -- catalog US
```

**지원 국가**: KR (한국), US (미국), CN (중국), JP (일본), HK (홍콩), VN (베트남)


## **7. 문서 (Documentation)**
상세한 설계 및 모듈별 설명은 `/docs` 디렉토리 참조
* [아키텍처 상세](/docs/architecture.md)
* [모듈별 설명](/docs/modules.md)
* [설정 가이드](/docs/configuration.md)
* [API 레퍼런스](/docs/api_reference.md)
