# yQuant Deployment Scripts

이 디렉토리는 yQuant 애플리케이션의 배포 및 관리를 위한 스크립트들을 포함합니다.

## 📁 디렉토리 구조

```
scripts/
├── deploy-engine.sh       # [Engine 서버] 전체 배포 (Core 엔진 서비스들)
├── deploy-dashboard.sh    # [Dashboard 서버] 전체 배포 (UI)
├── setup-engine.sh        # [Engine 서버] systemd 서비스 설치
├── setup-dashboard.sh     # [Dashboard 서버] systemd 서비스 설치
├── build-engine.sh        # Engine 관련 앱 빌드
├── build-dashboard.sh     # Dashboard 관련 앱 빌드
├── restart-engine.sh      # Engine 서비스 재시작
├── restart-dashboard.sh   # Dashboard 서비스 재시작
├── health-check-engine.sh # [Engine 서버] 서비스 상태 확인
├── health-check-dashboard.sh # [Dashboard 서버] 서비스 상태 확인
└── systemd/               # systemd 서비스 파일 템플릿
    ├── brokergateway.service
    ├── ordermanager.service
    ├── notifier.service
    ├── console-sync.service
    ├── console-sync.timer
    ├── web.service
    └── webhook.service
```

## 🌐 서버별 구성 및 배포

분산 환경(Engine + Dashboard)에서의 배포 프로세스입니다.

### 1. Engine 서버 (A1.Flex 등)
핵심 트레이딩 엔진과 Redis를 가동합니다.

#### 초기 설정
```bash
cd ~/yquant
# 1) 시스템 서비스 설치
bash scripts/setup-engine.sh
# 2) 서비스 활성화 및 시작
systemctl --user enable brokergateway ordermanager notifier webhook console-sync.timer
systemctl --user start brokergateway ordermanager notifier webhook console-sync.timer
```

#### 배포
```bash
bash scripts/deploy-engine.sh
```

### 2. Dashboard 서버 (E2.Micro 등)
대시보드 UI만 가동합니다.

**중요:** `/srv/yquant/web/appsecrets.json`에서 **Redis 주소를 Engine 서버의 IP**로 수정해야 합니다.

#### 초기 설정
```bash
cd ~/yquant
# 1) 시스템 서비스 설치
bash scripts/setup-dashboard.sh
# 2) 서비스 활성화 및 시작
systemctl --user enable web
systemctl --user start web
```

#### 배포
```bash
bash scripts/deploy-dashboard.sh
```

### 개별 스크립트 실행

#### 빌드만 수행 (각 서버에서)

```bash
# Engine 서버에서
bash scripts/build-engine.sh

# Dashboard 서버에서
bash scripts/build-dashboard.sh
```

#### 서비스 재시작만 수행 (각 서버에서)

```bash
# Engine 서버에서
bash scripts/restart-engine.sh

# Dashboard 서버에서
bash scripts/restart-dashboard.sh
```

#### 서비스 상태 확인 (각 서버에서)

```bash
# Engine 서버에서
bash scripts/health-check-engine.sh

# Dashboard 서버에서
bash scripts/health-check-dashboard.sh
```

## 🔧 GitHub Actions 설정

GitHub 저장소의 Settings > Secrets and variables > Actions에 다음 시크릿들을 추가하세요:

#### 1. Engine 서버용 시크릿
| Secret Name | 설명 |
|------------|------|
| `ENGINE_HOST` | Engine 서버 호스트 (A1) |
| `ENGINE_SSH_USER` | SSH 사용자명 |
| `ENGINE_SSH_KEY` | SSH 개인 키 |
| `ENGINE_SSH_PORT` | SSH 포트 (기본 22) |

#### 2. Dashboard 서버용 시크릿
| Secret Name | 설명 |
|------------|------|
| `DASHBOARD_HOST` | Dashboard 서버 호스트 (E2) |
| `DASHBOARD_SSH_USER` | SSH 사용자명 |
| `DASHBOARD_SSH_KEY` | SSH 개인 키 |
| `DASHBOARD_SSH_PORT` | SSH 포트 (기본 22) |

### SSH 키 생성 (서버에서)

```bash
# 서버에서 SSH 키 생성
ssh-keygen -t ed25519 -C "github-actions-deploy" -f ~/.ssh/github_deploy

# 공개 키를 authorized_keys에 추가
cat ~/.ssh/github_deploy.pub >> ~/.ssh/authorized_keys

# 개인 키 내용을 GitHub Secret에 추가
cat ~/.ssh/github_deploy
```

## 📊 유용한 명령어

### 서비스 상태 확인

```bash
# 모든 서비스 상태
systemctl --user status

# 특정 서비스 상태
systemctl --user status brokergateway

# 실시간 로그 확인
journalctl --user -u brokergateway -f

# 최근 로그 확인
journalctl --user -u brokergateway -n 50
```

### 타이머 확인

```bash
# 타이머 상태
systemctl --user status console-sync.timer

# 다음 실행 시간
systemctl --user list-timers console-sync.timer

# 마지막 실행 로그
journalctl --user -u console-sync.service -n 100
```

### 서비스 제어

```bash
# 서비스 중지
systemctl --user stop brokergateway

# 서비스 재시작
systemctl --user restart brokergateway

# 서비스 비활성화
systemctl --user disable brokergateway
```

## 🐛 트러블슈팅

### 배포 실패 시

1. **로그 확인**
   ```bash
   journalctl --user -u brokergateway -n 100
   ```

2. **권한 확인**
   ```bash
   ls -la /srv/yquant
   ```

3. **설정 확인**
   ```bash
   cat /srv/yquant/brokergateway/appsecrets.json
   ```

4. **Redis 연결 확인**
   ```bash
   docker ps | grep redis
   ```

### 서비스 파일 수정 후

```bash
# 데몬 리로드 필요
systemctl --user daemon-reload
systemctl --user restart brokergateway
```

### 설정 정보 변경 후

```bash
# appsecrets.json 파일 수정 후 서비스 재시작 (해당 서버에서)
bash scripts/restart-engine.sh  # Engine 서버일 경우
bash scripts/restart-dashboard.sh     # Dashboard 서버일 경우
```

## 📝 참고사항

- 모든 스크립트는 실행 권한이 필요합니다: `chmod +x scripts/*.sh`
- 배포 경로 기본값: `/srv/yquant`
- systemd 사용자 서비스 디렉토리: `~/.config/systemd/user`
- 로그 저장 위치: `~/.local/share/systemd/journal/` (systemd-journald)

## 🔗 관련 문서

- [setup-systemd-services.md](../docs/setup-systemd-services.md) - systemd 서비스 상세 설정 가이드
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
