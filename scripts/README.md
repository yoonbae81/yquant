# yQuant Operational Scripts

이 디렉토리는 yQuant 애플리케이션의 운영을 위한 스크립트들을 포함합니다. 서버의 역할(Worker vs Gateway)에 따라 디렉토리가 구분되어 있습니다.

## 📁 디렉토리 구조

```
scripts/
├── worker/               # yq-blue, yq-green 서버용 (핵심 서비스)
│   ├── build.sh        # 서비스 빌드 (옵션: 특정 서비스명)
│   ├── setup.sh        # systemd 서비스 등록
│   ├── restart.sh      # 서비스 재시작 (옵션: 특정 서비스명)
│   ├── deploy.sh       # 통합 배포 (옵션: 특정 서비스명)
│   └── health-check.sh # 상태 점검
├── gateway/               # yq-gateway 서버용 (Catalog Sync 전용)
│   ├── build.sh
│   ├── setup.sh
│   ├── restart.sh
│   ├── deploy.sh
│   └── health-check.sh
├── switch-active.sh    # Active 워커 전환 (HAProxy)
└── systemd/            # systemd 서비스 파일 템플릿
```

## 🚀 사용법

### 1. 워커 노드 (Worker - Blue/Green)
핵심 로직이 돌아가는 서버에서 사용합니다.

```bash
# 전체 서비스 배포
bash scripts/worker/deploy.sh

# 특정 서비스(예: dashboard)만 빠르게 배포
bash scripts/worker/deploy.sh dashboard
```

### 2. 게이트웨이 노드 (Gateway)
마스터 데이터 동기화만 담당하는 서버에서 사용합니다. 인자 없이 실행합니다.

```bash
# Catalog Sync 빌드 및 배포
bash scripts/gateway/deploy.sh
```

## �🟢 Blue/Green 배포 전략

yQuant의 핵심 엔진 워커는 **Blue/Green** 방식으로 운영되어 무중단 배포 및 고가용성을 유지합니다.

### 1. 워커 역할
- **Active 워커**: 현재 실 서비스 트래픽(Webhook, Dashboard 등)을 처리 중인 노드.
- **Standby 워커**: 새로운 버전이 배포되었거나 대기 중인 노드. HAProxy에 의해 백업으로 설정되어 있습니다.

### 2. 배포 및 전환 워크플로우
1. **Standby 워커 업데이트**: Standby 상태인 노드(예: green)에 먼저 배포를 수행합니다.
   ```bash
   # green 노드에서 실행
   bash scripts/worker/deploy.sh
   ```
2. **상태 확인**: 배포된 노드의 헬스체크를 수행합니다.
   ```bash
   bash scripts/worker/health-check.sh
   ```
3. **Active 전환**: 모든 점검이 완료되면 HAProxy 설정을 변경하여 트래픽을 Standby였던 노드로 전환합니다.
   ```bash
   # HAProxy가 설치된 관리 서버에서 실행
   bash scripts/switch-active.sh green
   ```

### 3. Active 워커 확인
현재 어떤 노드가 Active인지 확인하려면 `health-check.sh`를 실행하십시오.
```bash
bash scripts/worker/health-check.sh
# 출력 예: 📍 Worker: yq-blue | Role: ACTIVE (Webhook Traffic)
```

---

## �🔧 서비스 목록 (Node)
`worker` 스크립트에서 인자로 사용할 수 있는 서비스명은 다음과 같습니다:
- `brokergateway`
- `ordermanager`
- `notifier`
- `webhook`
- `dashboard`

---

## 📊 서비스 관리 (systemd)

```bash
# 특정 서비스 로그 실시간 확인
journalctl --user -t brokergateway -f

# 모든 사용자 서비스 상태 요약
systemctl --user list-units --type=service
```
