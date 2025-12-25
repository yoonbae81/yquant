---
description: Setup systemd services for all yQuant applications on Arch Linux
---

# Arch Linux에서 yQuant 애플리케이션을 systemd 서비스로 설정하기

이 가이드는 모든 yQuant 애플리케이션을 Arch Linux에서 systemd 서비스로 설정하는 방법을 안내합니다.

## 애플리케이션 개요

| 애플리케이션 | 유형 | 설명 |
|------------|------|------|
| **BrokerGateway** | 상시 실행 서비스 | 브로커 API와 연동하여 주문 처리 및 계좌 정보 동기화 |
| **OrderManager** | 상시 실행 서비스 | 예약 주문 및 청산 스케줄 관리 |
| **Notifier** | 상시 실행 서비스 | Redis Pub/Sub을 통해 알림을 수신하여 Discord/Telegram으로 전송 |
| **Console** | 주기적 실행 (Timer) | 종목 마스터 데이터를 주기적으로 업데이트 |
| **Web** | 상시 실행 서비스 | Blazor 기반 웹 대시보드 UI |
| **Webhook** | 상시 실행 서비스 | TradingView 등 외부 시그널을 수신하는 웹훅 서버 |

## 사전 준비사항

1. .NET SDK가 설치되어 있어야 합니다
2. 프로젝트가 정상적으로 빌드되어야 합니다
3. Redis 도커 컨테이너가 실행 중이어야 합니다
4. 필요한 환경 변수와 설정 파일이 준비되어 있어야 합니다

## systemd User 모드

이 가이드는 **systemd user 모드**를 사용합니다. 시스템 전역 서비스(`/etc/systemd/system`) 대신 사용자 서비스(`~/.config/systemd/user`)로 실행하여 다음과 같은 장점이 있습니다:

**장점:**
- `sudo` 권한 불필요
- 사용자 권한으로 서비스 관리
- 사용자별 독립적인 서비스 구성
- 파일 권한 문제 최소화

## 설정 관리 방식

이 프로젝트는 **`appsecrets.json`**을 사용하여 설정을 관리합니다. 각 애플리케이션 유닛(Web, BrokerGateway 등)은 실행 시 배포 디렉토리에 있는 `appsecrets.json` 파일에서 Redis 연결 정보 및 API 키 등을 읽어옵니다.

**주요 설정 항목 (`Redis` 섹션):**
- **`Message`**: 각 유닛간의 메시징 및 로컬 캐시용 Redis 주소.
- **`Token`**: 전 환경 공용 KIS 토큰 저장용 Redis 주소 (Redis Cloud 등 공유 서비스 이용 권장).

> **Tip**: 보안을 위해 `appsecrets.json`은 소스 제어(Git)에서 제외되어 있으며, 운영 환경에 직접 배포하거나 비밀 관리 도구를 사용해야 합니다.

## 초기 설정 (한 번만 실행)

### 1단계: 애플리케이션 빌드

```bash
cd ~/yquant
bash scripts/build-all.sh
```

### 2단계: systemd 서비스 설치

```bash
bash scripts/setup-systemd.sh
```

이 스크립트는 다음을 자동으로 수행합니다:
- 모든 서비스 파일 설치
- systemd 데몬 리로드

### 3단계: 설정 파일 확인

모든 애플리케이션 디렉토리에 `appsecrets.json` 파일이 올바르게 위치하고 Redis 주소가 설정되어 있는지 확인하세요.

### 4단계: 서비스 활성화 및 시작

```bash
# 서비스 활성화 (부팅 시 자동 시작)
systemctl --user enable brokergateway ordermanager notifier web webhook console-sync.timer

# 서비스 시작
systemctl --user start brokergateway ordermanager notifier web webhook console-sync.timer

# 로그아웃 후에도 서비스 유지
sudo loginctl enable-linger $USER
```

## 배포 및 업데이트

### 자동 배포 (GitHub Actions)

태그를 푸시하면 자동으로 배포됩니다:

```bash
# 로컬에서 태그 생성 및 푸시
git tag v1.0.0
git push origin v1.0.0
```

또는 GitHub Actions에서 수동으로 트리거할 수 있습니다 (Actions 탭 → Deploy to Production → Run workflow).

**GitHub Secrets 설정 필요:**
- `DEPLOY_SERVER_HOST` - 배포 서버 호스트
- `DEPLOY_SSH_USER` - SSH 사용자명
- `DEPLOY_SSH_KEY` - SSH 개인 키
- `DEPLOY_SSH_PORT` - SSH 포트 (선택사항, 기본값: 22)

### 수동 배포 (서버에서)

```bash
cd ~/yquant
bash scripts/deploy.sh
```

이 스크립트는 다음을 자동으로 수행합니다:
1. 최신 코드 pull
2. 모든 애플리케이션 빌드
3. 모든 서비스 재시작
4. 서비스 상태 확인

### 개별 작업

```bash
# 빌드만 수행
bash scripts/build-all.sh

# 서비스 재시작만 수행
bash scripts/restart-services.sh

# 서비스 상태 확인
bash scripts/health-check.sh
```

## 서비스 관리

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

# 수동 실행
systemctl --user start console-sync.service
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

## 트러블슈팅

### 서비스가 시작되지 않는 경우

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

4. **의존성 확인**
   ```bash
   docker ps | grep redis
   ```

### 설정 정보 변경 후

```bash
# appsecrets.json 파일 수정 후 서비스 재시작
bash scripts/restart-services.sh
```

### 서비스 파일 수정 후

```bash
# 데몬 리로드 필요
systemctl --user daemon-reload
systemctl --user restart brokergateway
```

## 참고사항

### 서비스 실행 순서

권장 실행 순서:
1. Redis (Docker 컨테이너)
2. BrokerGateway (계좌 정보 제공)
3. OrderManager (주문 생성)
4. Notifier (알림 전송)
5. Webhook (신호 수신)
6. Web (대시보드)

systemd의 `After=` 지시어가 이를 자동으로 처리합니다.

### 리소스 사용량 모니터링

```bash
# 사용자 서비스 리소스 사용량 확인
systemd-cgtop --user

# 특정 서비스의 리소스 사용량
systemctl --user status brokergateway
```

### 로그 관리

```bash
# 특정 시간 이후 로그 보기
journalctl --user -u brokergateway --since "1 hour ago"

# 특정 날짜의 로그 보기
journalctl --user -u brokergateway --since "2025-12-14"

# 에러 로그만 보기
journalctl --user -u brokergateway -p err

# 로그를 파일로 저장
journalctl --user -u brokergateway > brokergateway.log
```

### 로그 로테이션

systemd-journald는 자동으로 로그를 관리합니다. 사용자 로그는 `~/.local/share/systemd/journal/`에 저장됩니다.

## 추가 정보

- **배포 스크립트 상세 가이드**: [`scripts/README.md`](../scripts/README.md)
- **systemd 서비스 파일 템플릿**: `scripts/systemd/`
- **GitHub Actions 워크플로우**: `.github/workflows/deploy.yml`
