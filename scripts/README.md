# yQuant Deployment Scripts

이 디렉토리는 yQuant 애플리케이션의 배포 및 관리를 위한 스크립트들을 포함합니다.

## 📁 디렉토리 구조

```
scripts/
├── build-all.sh           # 모든 애플리케이션 빌드
├── restart-services.sh    # 모든 서비스 재시작
├── deploy.sh              # 전체 배포 프로세스 (pull + build + restart)
├── health-check.sh        # 서비스 상태 확인
├── setup-systemd.sh       # systemd 서비스 파일 설치 (초기 설정용)
└── systemd/               # systemd 서비스 파일 템플릿
    ├── brokergateway.service
    ├── ordermanager.service
    ├── notifier.service
    ├── console-sync.service
    ├── console-sync.timer
    ├── web.service
    └── webhook.service
```

## 🚀 초기 설정 (서버에서 한 번만 실행)

### 1. systemd 서비스 설치

```bash
cd ~/yquant
bash scripts/setup-systemd.sh
```

이 스크립트는:
- systemd 사용자 서비스 디렉토리 생성 (`~/.config/systemd/user`)
- 모든 서비스 파일 복사 및 설치
- systemd 데몬 리로드

### 2. 설정 확인

운영 서버의 배포 경로(예: `/srv/yquant/`)에 `appsecrets.json` 파일이 위치하고 올바른 Redis 주소가 설정되어 있는지 확인하세요.

### 3. 서비스 활성화 및 시작

```bash
# 서비스 활성화 (부팅 시 자동 시작)
systemctl --user enable brokergateway ordermanager notifier web webhook console-sync.timer

# 서비스 시작
systemctl --user start brokergateway ordermanager notifier web webhook console-sync.timer

# 로그아웃 후에도 서비스 유지
sudo loginctl enable-linger $USER
```

## 🔄 배포 스크립트 사용법

### 자동 배포 (GitHub Actions)

태그를 푸시하면 자동으로 배포됩니다:

```bash
# 로컬에서
git tag v1.0.0
git push origin v1.0.0
```

또는 GitHub Actions에서 수동으로 트리거할 수 있습니다.

### 수동 배포 (서버에서 직접)

```bash
cd ~/yquant
bash scripts/deploy.sh
```

이 스크립트는 다음을 수행합니다:
1. 최신 코드 pull (`git pull origin main`)
2. 모든 애플리케이션 빌드 (`build-all.sh`)
3. 모든 서비스 재시작 (`restart-services.sh`)
4. 서비스 상태 확인

### 개별 스크립트 실행

#### 빌드만 수행

```bash
bash scripts/build-all.sh
```

환경 변수 `DEPLOY_ROOT`로 배포 경로를 변경할 수 있습니다:

```bash
DEPLOY_ROOT=/custom/path bash scripts/build-all.sh
```

#### 서비스 재시작만 수행

```bash
bash scripts/restart-services.sh
```

#### 서비스 상태 확인

```bash
bash scripts/health-check.sh
```

## 🔧 GitHub Actions 설정

GitHub 저장소의 Settings > Secrets and variables > Actions에 다음 시크릿을 추가하세요:

| Secret Name | 설명 | 예시 |
|------------|------|------|
| `SERVER_HOST` | 배포 서버 호스트 | `123.456.789.0` |
| `SSH_USER` | SSH 사용자명 | `yquant` |
| `SSH_KEY` | SSH 개인 키 | `-----BEGIN OPENSSH PRIVATE KEY-----...` |
| `SSH_PORT` | SSH 포트 (선택사항) | `22` (기본값) |

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
# appsecrets.json 파일 수정 후 서비스 재시작
bash scripts/restart-services.sh
```

## 📝 참고사항

- 모든 스크립트는 실행 권한이 필요합니다: `chmod +x scripts/*.sh`
- 배포 경로 기본값: `/srv/yquant`
- systemd 사용자 서비스 디렉토리: `~/.config/systemd/user`
- 로그 저장 위치: `~/.local/share/systemd/journal/` (systemd-journald)

## 🔗 관련 문서

- [setup-systemd-services.md](../docs/setup-systemd-services.md) - systemd 서비스 상세 설정 가이드
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
