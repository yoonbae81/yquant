# 배포 전략 (Deployment Strategy)

본 문서는 **yq-data**, **yq-blue**, **yq-green** 3개의 Worker를 활용한 인프라 구성 및 배포 운영 전략을 기술합니다.

## 1. 아키텍처 개요 (Blue/Green Architecture)

시스템은 데이터 저장 및 트래픽 분산을 담당하는 데이터 노드(`yq-data`)와 실제 애플리케이션이 구동되는 쌍둥이 Worker 노드(`yq-blue`/`yq-green`)로 구성됩니다.

```ascii
                                     [ Internet ]
                                          │
                                   (Webhook Traffic)
                                     HTTPS / 443
                                          │
                                          ▼
                                   ┌─────────────┐
                                   │   yq-data   │
                                   │ (HAProxy)   │
                                   │ (MariaDB)   │
                                   └──────┬──────┘
                                          │
                        ┌─────────────────┴─────────────────┐
                        │          (Internal Network)       │
                        ▼                                   ▼
               ┌─────────────────┐                 ┌─────────────────┐
               │     yq-blue     │                 │    yq-green     │
               │ (Active/Standby)│                 │ (Active/Standby)│
               │                 │                 │                 │
               │  [WebApp]       │                 │  [WebApp]       │
               │  [Valkey P/S]   │                 │  [Valkey P/S]   │
               └────────▲────────┘                 └────────▲────────┘
                        │                                   │
                        └───────── (Tailscale VPN) ─────────┘
                                          ▲
                                          │
                                     [ Admin ]
                                (Dashboard Access)
```

## 2. 컴포넌트별 역할 및 구성

### 2.1. `yq-data` (데이터 노드)
*   **역할**: 영구 데이터 저장 및 트래픽 분산
*   **주요 컴포넌트**:
    *   **MariaDB**: 매매 이력, 종목 카탈로그, 예약 주문, 증권사 토큰 등 모든 영구 데이터를 저장하는 공유 데이터베이스입니다.
    *   **Console Catalog Sync**: 정기적으로 종목 마스터 데이터를 외부 소스에서 동기화하여 MariaDB에 저장합니다.
    *   **HAProxy**: 외부 트래픽(TradingView Webhook)을 받아 Active 상태인 Worker(`yq-blue` 또는 `yq-green`)로 라우팅합니다.
    *   **Let's Encrypt (Certbot)**: SSL/TLS 인증서를 발급 및 갱신하여 TLS Termination을 수행합니다.
*   **특징**:
    *   가장 가벼운 사양(예: E2.Micro)으로 운영 가능합니다.
    *   데이터 저장 및 동기화가 주 역할이며, 애플리케이션 로직은 수행하지 않습니다.

### 2.2. `yq-blue` / `yq-green` (Worker 노드)
*   **역할**: 실제 트레이딩 로직과 대시보드를 수행하는 격리된 환경
*   **동작 방식**:
    *   두 Worker는 동일한 스펙과 소프트웨어 구성을 가집니다.
    *   한 시점에는 하나의 Worker만 **Active** 상태로 외부 Webhook 트래픽을 처리합니다.
    *   다른 Worker는 **Standby** 상태로 차기 배포 대기 또는 점검 목적으로 유지됩니다.
*   **Valkey 구성 (Independent Pub/Sub)**:
    *   각 Worker는 로컬에 **독립적인 Valkey 인스턴스**를 가집니다.
    *   이 Valkey는 오직 해당 Worker 내의 프로세스 간 메시지 전달(Pub/Sub) 용도로만 사용됩니다.
    *   **데이터 동기화 없음**: `blue`와 `green` 간의 Valkey 데이터 복제나 클러스터링을 하지 않습니다. 이는 배포 간 상태 간섭을 원천 차단하기 위함입니다.
    *   **Shared DB 연동**: 모든 Worker는 `yq-data` 노드의 **MariaDB**에 접속하여 영구 데이터를 공유합니다. 종목 카탈로그는 구동 시 DB에서 로드하여 로컬 메모리로 캐싱합니다.

## 3. 네트워크 및 접근 제어

### 3.1. 외부 트래픽 (TradingView Webhook)
*   **경로**: `Internet` → `yq-data` (443) → `Active Worker` (Internal IP:WebPort)
*   **보안**:
    *   `yq-data`에서 SSL 복호화(Termination) 수행.
    *   `yq-data`와 Worker 노드 간은 내부 사설망 통신.
    *   HAProxy 설정을 통해 오직 Webhook 관련 경로만 허용하고 기타 접근은 차단합니다.

### 3.2. 내부 접근 (Dashboard & SSH)
*   **수단**: **Tailscale** (Mesh VPN)
*   **경로**: `Admin PC` → `Tailscale Tunnel` → `yq-blue/green` (Private)
*   **정책**:
    *   대시보드(Web UI)는 공용 인터넷에 노출되지 않습니다.
    *   관리자는 Tailscale을 켜고 `http://yq-blue:2000` 또는 `http://yq-green:2000` 형태로 직접 접속합니다.
    *   이를 통해 외부 공격 위협을 최소화하고, 별도의 인증 게이트웨이 없이 VPN 인증으로 보안을 대체합니다.

## 4. 배포 시나리오 (Blue-Green Deployment)

현재 `yq-blue`가 **Active** 상태라고 가정할 때, 새로운 버전 배포 절차는 다음과 같습니다.

1.  **배포 (Deploy)**
    *   Github Action 또는 수동 스크립트를 통해 `yq-green` (Standby) Worker에 최신 코드를 배포합니다.
    *   `yq-green` 내의 모든 서비스(Valkey, Web, Backend)를 재시작합니다.

2.  **검증 (Verify)**
    *   관리자는 Tailscale을 통해 `yq-green` 대시보드에 접속합니다.
    *   시스템 상태, 연결 정상 여부 등을 확인합니다. (이때 외부 신호는 아직 `blue`로 감)

3.  **전환 (Switch)**
    *   검증이 완료되면 `yq-data`의 HAProxy 설정을 수정하여 트래픽 백엔드를 `blue`에서 `green`으로 변경합니다.
    *   `systemctl reload haproxy`를 통해 무중단으로 설정을 적용합니다.
    *   이제 TradingView의 Webhook 신호가 `yq-green`으로 유입됩니다.

4.  **대기 (Standby)**
    *   `yq-blue`는 이제 Standby 상태가 되며, 다음 배포 시 `Active`가 될 준비를 하고 대기합니다.

## 5. 포트 및 방화벽 설정 요약

| Worker | 프로토콜 | 포트 | 접근 허용 | 용도 |
| :--- | :--- | :--- | :--- | :--- |
| Worker | 포트 | 용도 | 접근 경로 | 방화 벽/보안 전략 |
| :--- | :--- | :--- | :--- | :--- |
| **`yq-data`** | **80, 443** | **외부 서비스** (HAProxy) | Internet → HAProxy | **VCN + firewalld Open (Any)** |
| `yq-data` | 3306 | MariaDB | Blue/Green → Data | VCN subnet (10.0.0.0/24) |
| `yq-data` | 41641 | Tailscale | P2P VPN | UDP Open (Any) |
| **`yq-blue/green`** | **6000** | **Webhook** (App) | HAProxy → App | **VCN subnet (10.0.0.0/24)** |
| **`yq-blue/green`** | **2000** | **Dashboard** (App) | User → App | **Tailscale Only** (Admin) |
| `yq-blue/green` | 6379 | Message Valkey | Localhost Only | Loopback (127.0.0.1) |
| `yq-blue/green` | 41641 | Tailscale | P2P VPN | UDP Open (Any) |


*참고: 모든 Worker는 기본적으로 SSH 접근을 Tailscale 내부 IP로만 제한하는 것을 권장합니다.*

## 6. 핵심 설정 예시

### 6.1. HAProxy 설정 (`/etc/haproxy/haproxy.cfg`)
Active/Standby 전환을 위한 HAProxy 백엔드 설정 예시입니다.

```haproxy
frontend yquant_https
    bind *:443 ssl crt /etc/haproxy/certs/yquant.pem
    default_backend yquant_backend

backend yquant_backend
    mode http
    option http-server-close
    option forwardfor
    
    # Active Worker (예: Blue가 Active일 경우)
    server blue yq-blue:80 check cookie blue
    
    # Standby Node (Backup 옵션으로 평소엔 트래픽 받지 않음)
    server green yq-green:80 check backup cookie green
```

## 7. 운영 및 관리 스크립트

시스템 관리를 위해 `/scripts` 디렉토리에 통합 스크립트가 준비되어 있습니다.

*   **`setup.sh`**: 모든 서비스(Frontend, Backend, Sync)의 systemd 유닛 파일을 설치하고 초기 환경을 구성합니다.
*   **`deploy.sh`**: 현재 Worker에 최신 코드를 빌드하고 모든 서비스를 재시작합니다.
*   **`health-check.sh`**: 엔진 서비스, 웹, Valkey의 통합 상태를 점검합니다.
*   **`switch-active.sh`**: `yq-data` 서버에서 HAProxy 설정을 변경하여 Active Worker를 전환합니다.

## 8. 장애 대응 (Failover)

1.  **애플리케이션 장애**: 
    *   HAProxy가 `check` 기능을 통해 Active Worker의 장애를 감지하면, 자동으로 Backup(Standby) Worker로 트래픽을 넘길 수 있습니다.
    *   완전한 전환을 위해서는 `switch-active.sh`를 실행하여 명시적으로 Active를 교체하는 것을 권장합니다.

2.  **Valkey 장애**: 
    *   각 Worker는 로컬 Pub/Sub Valkey를 사용하므로, Blue Worker의 Valkey 장애 시 Green Worker로 전환하면 즉시 정상화됩니다.
    *   MariaDB (`yq-data` 노드) 장애 시에는 신규 데이터 저장이 제한되나, 이미 로드된 메모리 데이터로 트레이딩은 지속 가능합니다.

3.  **데이터 노드 장애**: 
    *   DNS를 `yq-blue` 또는 `yq-green`의 공인 IP로 임시 변경하고, 해당 Worker의 방화벽을 개방하여 비상 운영합니다.
    *   MariaDB 백업에서 복구하거나, 임시로 Worker 노드에 MariaDB를 설치하여 운영할 수 있습니다.
