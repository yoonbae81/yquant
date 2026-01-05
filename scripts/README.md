# yQuant Operational Scripts

이 디렉토리는 yQuant 애플리케이션의 빌드, 설치, 배포 및 통합 관리를 위한 단일화된 스크립트들을 포함합니다.

## 📁 디렉토리 구조

```
scripts/
├── build.sh            # 애플리케이션 빌드
├── setup.sh            # systemd 서비스 및 타이머 설치
├── restart.sh          # 서비스 재시작
├── health-check.sh     # 서비스 및 Valkey/Sentinel 상태 점검
├── deploy.sh           # 로컬 노드 배포 (build + restart)
├── switch-active.sh    # Active 노드 전환 (HAProxy 설정 업데이트)
├── haproxy/            # HAProxy 설정 예시
└── systemd/            # systemd 서비스 파일 템플릿
```

## 🚀 주요 스크립트 사용법

모든 주요 스크립트(`build.sh`, `setup.sh`, `restart.sh`, `deploy.sh`, `health-check.sh`)는 실행 시 대상 환경을 인자로 받을 수 있습니다.

### 인자값 설명
- **인자 없음**: 모든 서비스를 대상으로 실행 (기본값)
- **`port`**: `yq-port` 서버용 - **Console Catalog (Sync Tool)** 관련만 처리
- **`node`**: `yq-blue/green` 서버용 - Console Catalog를 **제외한 모든 서비스** 처리

---

### 1. 초기 설정 (`setup.sh`)
새로운 노드에서 systemd 서비스를 최초로 등록할 때 사용합니다.
```bash
# Port 서버 설정 (Catalog Sync만)
bash scripts/setup.sh port

# Node 서버 설정 (Catalog 제외 전 서비스)
bash scripts/setup.sh node
```

### 2. 통합 빌드 (`build.sh`)
.NET 프로젝트를 빌드하고 배포용 바이너리를 생성합니다.
```bash
# 특정 환경만 빌드
bash scripts/build.sh port
```

### 3. 노드 배포 (`deploy.sh`)
로컬 노드에서 `build.sh`와 `restart.sh`를 순차적으로 실행합니다.
```bash
# Node 서버 배포
bash scripts/deploy.sh node
```

### 4. 상태 점검 (`health-check.sh`)
애플리케이션 서비스, Valkey 상태를 점검합니다.
```bash
# Port 서버 상태 점검
bash scripts/health-check.sh port
```

### 5. Active 노드 전환 (`switch-active.sh`)
배포 완료 후, HAProxy의 백엔드 설정을 변경하여 실 서비스 트래픽을 전환합니다.
```bash
# Green 노드를 Active로 전환
bash scripts/switch-active.sh green
```

## 🔧 GitHub Actions 연동

`.github/workflows/deploy.yml` 워크플로우에서 각 서버 타입에 맞는 인자를 사용하여 배포를 수행합니다.

## 📊 서비스 관리 (systemd)

```bash
# 특정 서비스 로그 실시간 확인
journalctl --user -t brokergateway -f

# 모든 사용자 서비스 상태 요약
systemctl --user list-units --type=service
```
