# yQuant Operational Scripts

이 디렉토리는 yQuant 애플리케이션의 빌드, 설치, 배포 및 통합 관리를 위한 단일화된 스크립트들을 포함합니다.

## 📁 디렉토리 구조

```
scripts/
├── build.sh            # 모든 애플리케이션 빌드
├── setup.sh            # systemd 서비스 및 타이머 설치
├── restart.sh          # 모든 서비스 재시작
├── health-check.sh     # 서비스 및 Valkey/Sentinel 상태 점검
├── deploy.sh           # 로컬 노드 배포 (build + restart)
├── switch-active.sh    # Active 노드 전환 (HAProxy 설정 업데이트)
├── haproxy/            # HAProxy 설정 예시
├── valkey/             # Valkey/Sentinel 설정 예시
└── systemd/            # systemd 서비스 파일 템플릿
```

## 🔵🟢 Blue/Green 배포 아키텍처

yQuant는 무중단 배포와 고가용성을 위해 Blue/Green 모델을 채택하고 있습니다.

- **yq-port**: HAProxy (L7 Load Balancer) 및 Storage Valkey 운영
- **yq-blue**: Blue 환경 (애플리케이션 전체 운영)
- **yq-green**: Green 환경 (애플리케이션 전체 운영)

### 고가용성 구성 (HA)
- **HAProxy**: Blue/Green 노드 간 트래픽 라우팅 및 장애 감지
- **Valkey Sentinel**: Message Valkey 클러스터의 마스터 선정 및 Failover 자동화

## 🚀 주요 스크립트 사용법

모든 스크립트는 프로젝트 루트 디렉토리에서 실행해야 합니다.

### 1. 초기 설정 (`setup.sh`)
새로운 노드에서 systemd 서비스를 최초로 등록할 때 사용합니다.
```bash
bash scripts/setup.sh
```

### 2. 통합 빌드 (`build.sh`)
모든 .NET 프로젝트를 빌드하고 배포용 바이너리를 생성합니다.
```bash
bash scripts/build.sh
```

### 3. 노드 배포 (`deploy.sh`)
로컬 노드에서 `build.sh`와 `restart.sh`를 순차적으로 실행하여 배포를 완료합니다. GitHub Actions에서 자동으로 호출됩니다.
```bash
bash scripts/deploy.sh
```

### 4. 상태 점검 (`health-check.sh`)
애플리케이션 서비스, Valkey 마스터/슬레이브 상태, Sentinel 모니터링 상태를 종합적으로 점검합니다.
```bash
bash scripts/health-check.sh
```

### 5. Active 노드 전환 (`switch-active.sh`)
배포 완료 후, HAProxy의 백엔드 설정을 변경하여 실 서비스 트래픽을 전환합니다.
```bash
# Green 노드를 Active로 전환
bash scripts/switch-active.sh green
```

## 🔧 GitHub Actions 연동

`.github/workflows/deploy.yml` 워크플로우를 통해 Blue 또는 Green 노드에 원클릭 배포가 가능합니다.

### 필수 Secrets 설정
- `YQUANT_HOST_BLUE`: Blue 노드 IP/호스트
- `YQUANT_HOST_GREEN`: Green 노드 IP/호스트
- `YQUANT_SSH_USER`: SSH 접속 계정
- `YQUANT_SSH_KEY`: SSH 개인 키
- `YQUANT_SSH_PORT`: SSH 포트 (기본 22)

## 📊 서비스 관리 (systemd)

스크립트 내부적으로 사용되거나 수동 관리에 유용한 명령어들입니다.

```bash
# 특정 서비스 로그 실시간 확인
journalctl --user -t brokergateway -f

# 모든 사용자 서비스 상태 요약
systemctl --user list-units --type=service
```

## 🐛 트러블슈팅

1. **Valkey 연결 실패**: `valkey-cli ping`으로 응답 확인 및 `appsecrets.json` 설정 재확인.
2. **권한 오류**: 스크립트 실행 권한(`chmod +x scripts/*.sh`) 및 `loginctl enable-linger` 설정 확인.
3. **HAProxy 전환 미반영**: `yq-port` 서버에서 HAProxy 설정 파일 및 서비스 상태 확인.

## 📝 참고사항

- 모든 애플리케이션은 **systemd user mode**로 실행됩니다.
- 배포 경로는 기본적으로 사용자의 홈 디렉토리를 기준으로 합니다.
- `appsecrets.json`은 보안상 Git에 포함되지 않으므로 각 노드에 수동으로 배치해야 합니다.
