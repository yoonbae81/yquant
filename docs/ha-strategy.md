# High Availability (HA) & Blue-Green Strategy

yQuant 시스템은 자본의 안정적인 운용을 위해 3대의 인스턴스를 활용한 고가용성(HA) 구조를 지원합니다. 이 문서는 구체적인 인프라 구성과 무중단 배포 전략을 설명합니다.

## 1. 인프라 아키텍처 (3-VM Pair)

| 인스턴스 명 | 사양 (OCI) | 주요 역할 | 핵심 컴포넌트 |
| :--- | :--- | :--- | :--- |
| **`yq-gate`** | E2.Micro (1 OCPU, 1GB) | 시스템 관문 및 토큰 금고 | HAProxy, Valkey (Token), Sentinel |
| **`yq-blue`** | A1.Flex (2 OCPU, 12GB) | 액티브/스탠바이 엔진 | yQuant Apps, Valkey (Msg-Master), Sentinel |
| **`yq-green`** | A1.Flex (2 OCPU, 12GB) | 액티브/스탠바이 엔진 | yQuant Apps, Valkey (Msg-Replica), Sentinel |

### 상세 구성 이점
*   **Valkey 이원화**:
    *   **Message Valkey**: `blue`/`green`에 위치하며 실시간 주문 메시징을 담당 (초저지연). Sentinel에 의해 자동 페일오버 수행.
    *   **Token Valkey**: `gate`에 위치하며 증권사 인증 토큰 보관. 엔진 서버가 교체되어도 인증 상태 유지.
*   **Sentinel 중재**: 3개 노드에서 분산 구동되어 과반수(Quorum) 투표를 통해 안전하게 마스터를 선출합니다.

## 2. Blue-Green 배포 프로세스

이 방식은 운영 중인 서버(Active)를 건드리지 않고, 대기 서버(Standby)에 먼저 배포하여 검증한 뒤 역할을 교체합니다.

### 2.1. 단계별 실행
1.  **Code Push**: GitHub `main` 브랜치에 코드가 푸시됩니다.
2.  **Staging Deploy**: CI/CD 파이프라인이 현재 Standby인 노드(예: `green`)를 식별하여 신규 버전을 배포합니다.
3.  **Smoke Test**: `yq-gate`에서 제공하는 스테이징 전용 경로(예: `8080` 포트)를 통해 `green` 노드의 대시보드와 로그를 확인합니다.
4.  **Traffic Switch**: 이상이 없으면 HAProxy 설정을 업데이트하여 실서비스 트래픽(Port 80/443)을 `green`으로 전환합니다.
5.  **Master Promotion**: 필요 시 `green`의 Valkey를 마스터로 승격시킵니다.
6.  **Cleanup**: 기존 Active였던 `blue`를 스탠바이로 전환하고 동기화 상태로 둡니다.

## 3. 핵심 설정 가이드

### 3.1. HAProxy (`/etc/haproxy/haproxy.cfg`)
```haproxy
backend yquant_backend
    balance roundrobin
    cookie SERVERID insert indirect nocache
    server blue yq-blue-ip:5000 check cookie blue
    server green yq-green-ip:5000 check backup cookie green
```

### 3.2. Valkey Sentinel
각 노드에서 `valkey-sentinel.conf`를 실행하여 `mymaster`라는 이름으로 `blue/green`의 메시징 Valkey를 감시합니다.
```conf
sentinel monitor mymaster <blue-ip> 6379 2
sentinel down-after-milliseconds mymaster 5000
sentinel failover-timeout mymaster 60000
```

## 4. 운영 및 관리 스크립트

시스템 관리를 위해 `/scripts` 디렉토리에 통합 스크립트가 준비되어 있습니다.

*   **`setup.sh`**: 모든 서비스(Frontend, Backend, Sync)의 systemd 유닛 파일을 설치합니다.
*   **`deploy.sh`**: 현재 노드에 최신 코드를 빌드하고 모든 서비스를 재시작합니다.
*   **`health-check.sh`**: 엔진 서비스, 웹, Valkey 및 Sentinel의 통합 상태를 점검합니다.
*   **`switch-active.sh`**: `yq-gate` 서버에서 Active 노드를 전환할 때 사용합니다.

## 5. 장애 대응 (Failover)

1.  **애플리케이션 장애**: HAProxy가 `check` 기능을 통해 자동으로 살아있는 노드로 트래픽을 넘깁니다.
2.  **Valkey 장애**: Sentinel이 5초 이내에 장애를 감지하고 다른 노드를 마스터로 승격시킵니다. 앱은 Sentinel에 재접속하여 새 마스터를 찾습니다.
3.  **Gate 장애**: 인프라 수준의 복구가 필요하며, 복구 전까지 `blue`/`green` 중 마스터를 수동으로 지정하여 운영할 수 있습니다.
