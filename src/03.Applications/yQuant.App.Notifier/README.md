# yQuant.App.Notifier

Redis Pub/Sub 채널을 구독하여 시스템의 모든 operational notification을 Discord 및 Telegram으로 전송하는 전용 서비스입니다.

## 기능

- Redis 채널 구독: `notifications:orders`, `notifications:schedules`, `notifications:positions`, `notifications:system`
- 메시지 타입별 라우팅 (Discord/Telegram)
- **Telegram 필터링**: 중요한 메시지만 Telegram으로 전송
- Rate limiting 및 retry 로직
- 계정별 Discord webhook 분리

## 설정

### appsettings.json
```json
{
  "Notifier": {
    "MessageRouting": {
      "Orders": { 
        "Targets": ["Discord"],
        "TelegramFilter": []
      },
      "Schedules": { 
        "Targets": ["Discord"],
        "TelegramFilter": []
      },
      "Positions": { 
        "Targets": ["Discord"],
        "TelegramFilter": []
      },
      "System": { 
        "Targets": ["Discord", "Telegram"],
        "TelegramFilter": [
          "OrderFailed",
          "ScheduleFailed",
          "CriticalError"
        ]
      }
    },
    "Discord": {
      "Enabled": true,
      "TimeoutMs": 3000,
      "RetryCount": 2,
      "RateLimitPerMinute": 30
    },
    "Telegram": {
      "Enabled": true,
      "TimeoutMs": 3000,
      "RetryCount": 2
    }
  }
}
```

### appsecrets.json
Discord webhook URLs 및 Telegram credentials 포함 (민감 정보)

## Telegram 알림 전략

Telegram은 **주의가 필요한 최소한의 메시지만** 받도록 설정:

| 메시지 타입 | Discord | Telegram | 설명 |
|------------|---------|----------|------|
| 주문 성공 | ✅ | ❌ | 정상 운영 |
| **주문 실패** | ✅ | ✅ | 즉시 대응 필요 |
| 스케줄 실행 | ✅ | ❌ | 정상 운영 |
| **스케줄 실패** | ✅ | ✅ | 재설정 필요 |
| 앱 시작/종료 | ✅ | ❌ | 정보성 |
| **Critical Error** | ✅ | ✅ | 즉시 대응 필요 |

## 실행

```bash
# 개발 환경
dotnet run

# 프로덕션 (systemd)
sudo systemctl start yquant-notifier
sudo systemctl status yquant-notifier
```

## 테스트

Redis에 테스트 메시지 발행:

```bash
redis-cli

# System 메시지 테스트 (Discord만)
PUBLISH notifications:system '{"Type":"TestMessage","Data":{"message":"Hello from Notifier"}}'

# 실패 메시지 테스트 (Discord + Telegram)
PUBLISH notifications:system '{"Type":"OrderFailed","AccountAlias":"Trading","Data":{"ticker":"AAPL","reason":"Insufficient funds"}}'

# Order 메시지 테스트
PUBLISH notifications:orders '{"Type":"OrderExecuted","AccountAlias":"Trading","Data":{"ticker":"AAPL","action":"Buy","quantity":10}}'
```

## 문서

자세한 내용은 `/docs/notifier.md` 참조

## 아키텍처

```
Redis Pub/Sub → Worker → MessageRouter → DiscordLogger → Discord
                                       → TelegramNotificationService → Telegram (필터링됨)
```

## 의존성

- `yQuant.Infra.Notification`: 공통 notification 모델
- `yQuant.Infra.Notification.Discord`: Discord 전송 서비스
- `yQuant.Infra.Notification.Telegram`: Telegram 전송 서비스
- `yQuant.Infra.Redis`: Redis 연결 및 Pub/Sub
- `StackExchange.Redis`: Redis 클라이언트
