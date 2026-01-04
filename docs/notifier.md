# Notifier

## 개요

`yQuant.App.Notifier`는 시스템의 모든 operational notification을 중앙에서 처리하여 Discord 및 Telegram으로 전송하는 전용 서비스입니다.

## 아키텍처
`yQuant.App.Notifier`는 `yQuant.Infra.Notification` 라이브러리를 기반으로 구축되어, 다른 애플리케이션과 유기적으로 연동됩니다.

```
┌─────────────────┐
│ OrderManager    │ ──┐
│ BrokerGateway   │ ──┤
│ Web             │ ──┼─→ Valkey Pub/Sub ─→ Notifier  ──→ Discord
│ Console         │ ──┤   (notifications:*) (infra-based)  Telegram
└─────────────────┘   ┘

Critical Errors ────────────────────────────────────────→ Discord (직접 via Infra)
                                                          Telegram (직접 via Infra)
```

## 메시지 타입

### 1. **Orders** (주문 관련)
Valkey 채널: `notifications:orders`

**메시지 종류:**
- 주문 요청 (Buy/Sell)
- 주문 체결 성공
- 주문 실패/거부

**발신자:**
- `OrderManager`: 주문 요청 발행
- `BrokerGateway`: 주문 체결 결과

**Discord 라우팅:**
- `Accounts[accountAlias]` webhook 사용
- 계정별로 분리된 채널에 전송

---

### 2. **Schedules** (스케줄 관련)
Valkey 채널: `notifications:schedules`

**메시지 종류:**
- 스케줄 등록/수정/삭제
- 스케줄 실행 시작
- 스케줄 실행 성공/실패
- 다음 실행 시간 계산

**발신자:**
- `OrderManager`: 스케줄 관리 및 실행

**Discord 라우팅:**
- `Accounts[accountAlias]` webhook 사용
- 해당 계정의 채널에 전송

---

### 3. **Positions** (포지션 관련)
Valkey 채널: `notifications:positions`

**메시지 종류:**
- 포지션 변경 (신규 진입/청산)
- 손익 업데이트
- 포트폴리오 리밸런싱

**발신자:**
- `BrokerGateway`: 포지션 동기화 시

**Discord 라우팅:**
- `Accounts[accountAlias]` webhook 사용
- 계정별 포지션 현황 전송

---

### 4. **System** (시스템 상태)
Valkey 채널: `notifications:system`

**메시지 종류:**
- ✅ 애플리케이션 시작 (`LogStartupAsync`)
  - "OrderManager v1.0.0 started"
  - "BrokerGateway v1.0.0 started"
  
- ✅ 작업 완료 알림 (`LogStatusAsync`)
  - "Stock Catalog Sync Completed for KR"
  - "Daily Report Generated"
  - "Market Closed - Trading Suspended"

- ✅ 정상 종료
  - "OrderManager shutting down gracefully"

**발신자:**
- 모든 애플리케이션

**Discord 라우팅:**
- `System.Status` webhook 사용
- 시스템 전체 상태 채널에 전송

---

### 5. **Critical Errors** (치명적 오류)
⚠️ **주의: Notifier를 거치지 않고 각 패키지에서 직접 Discord/Telegram으로 전송**

**메시지 종류:**
- ❌ **Startup 실패**
  - Valkey 연결 실패
  - 설정 파일 로드 실패 (`appsettings.json`, `appsecrets.json`)
  - 브로커 인증 실패 (KIS API 토큰 발급 실패)
  - 필수 서비스 등록 실패

- ❌ **Runtime Critical**
  - Unhandled Exception
  - Valkey 연결 끊김 (재연결 실패)
  - 메모리 부족, 리소스 고갈

- ❌ **Data Integrity**
  - 주문 상태 불일치 감지
  - 포지션 데이터 손실
  - 중요 데이터 직렬화/역직렬화 실패

**발신자:**
- 모든 애플리케이션 (각자 직접 전송)

**전송 방식:**
- `ISystemLogger.LogSystemErrorAsync()` 사용
- Discord `System.Error` webhook + Telegram `ChatIds.Critical`로 동시 전송
- Valkey를 거치지 않음 (Valkey 장애 시에도 알림 보장)

---

## 설정

### appsettings.json (공개 설정)

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
      "RetryDelayMs": 1000,
      "RateLimitPerMinute": 30
    },
    "Telegram": {
      "Enabled": true,
      "TimeoutMs": 3000,
      "RetryCount": 2,
      "RetryDelayMs": 1000
    },
    "Performance": {
      "BatchSize": 10,
      "BatchDelayMs": 100,
      "MaxQueueSize": 1000
    }
  }
}
```

### appsecrets.json (민감 정보)

```json
{
  "Notifier": {
    "Discord": {
      "System": {
        "Status": "https://discord.com/api/webhooks/...",
        "Error": "https://discord.com/api/webhooks/..."
      },
      "Accounts": {
        "Trading": "https://discord.com/api/webhooks/...",
        "ISA": "https://discord.com/api/webhooks/...",
        "Pension": "https://discord.com/api/webhooks/...",
        "IRP": "https://discord.com/api/webhooks/...",
        "Yoonseo": "https://discord.com/api/webhooks/..."
      },
      "Default": "https://discord.com/api/webhooks/..."
    },
    "Telegram": {
      "BotToken": "your-bot-token",
      "ChatIds": {
        "Critical": "268911454",
        "System": "268911454",
        "Default": "268911454"
      }
    }
  }
}
```

## 설정 항목 설명

### MessageRouting
메시지 타입별로 어떤 채널(Discord/Telegram)로 전송할지 지정합니다.

- `Targets`: 전송할 채널 목록 (`["Discord"]`, `["Telegram"]`, `["Discord", "Telegram"]`, `[]`)
  - 빈 배열(`[]`)로 설정하면 해당 메시지 타입의 알림을 비활성화할 수 있습니다.

- `TelegramFilter`: Telegram으로 전송할 메시지 타입 필터 (선택적)
  - 빈 배열(`[]`)이면 모든 메시지를 Telegram으로 전송
  - 특정 메시지 타입만 지정하면 해당 타입만 Telegram으로 전송
  - **예시**: `["AppStartup", "AppShutdown", "OrderFailed"]` - 앱 시작/종료 및 주문 실패만 Telegram 알림

**Telegram 필터 사용 예:**
```json
{
  "System": {
    "Targets": ["Discord", "Telegram"],
    "TelegramFilter": [
      "OrderFailed",     // 주문 실패 시
      "ScheduleFailed",  // 스케줄 실패 시
      "CriticalError"    // 치명적 오류 시
    ]
  }
}
```
→ System 채널의 모든 메시지는 Discord로 전송되지만, Telegram은 위 3가지 타입만 받음


### Discord
- `Enabled`: Discord 알림 활성화 여부
- `TimeoutMs`: Discord API 호출 타임아웃 (밀리초)
- `RetryCount`: 실패 시 재시도 횟수
- `RetryDelayMs`: 재시도 간격 (밀리초)
- `RateLimitPerMinute`: Discord rate limit 방지 (분당 최대 전송 수)

### Telegram
- `Enabled`: Telegram 알림 활성화 여부
- `TimeoutMs`: Telegram API 호출 타임아웃 (밀리초)
- `RetryCount`: 실패 시 재시도 횟수
- `RetryDelayMs`: 재시도 간격 (밀리초)

### Performance
- `BatchSize`: 메시지 배치 처리 크기 (성능 최적화)
- `BatchDelayMs`: 배치 대기 시간 (밀리초)
- `MaxQueueSize`: 최대 큐 크기 (메모리 보호)

## 메시지 라우팅 로직

### Discord Webhook 선택

```csharp
string GetDiscordWebhook(NotificationMessage msg)
{
    // 1. Account 관련 메시지 → Accounts[alias]
    if (!string.IsNullOrEmpty(msg.AccountAlias))
    {
        if (_config.Discord.Accounts.TryGetValue(msg.AccountAlias, out var url))
            return url;
    }
    
    // 2. System 메시지 → System.Status
    if (msg.Channel == "notifications:system")
        return _config.Discord.System.Status;
    
    // 3. Fallback → Default
    return _config.Discord.Default;
}
```

### Telegram ChatId 선택

```csharp
string GetTelegramChatId(NotificationMessage msg)
{
    // 1. System 메시지 → ChatIds.System
    if (msg.Channel == "notifications:system")
        return _config.Telegram.ChatIds.System;
    
    // 2. Fallback → Default
    return _config.Telegram.ChatIds.Default;
}
```

## 사용 예시

### 다른 패키지에서 Notification 발행

```csharp
// OrderManager에서 스케줄 실행 알림
var message = new NotificationMessage
{
    Channel = "notifications:schedules",
    Type = "ScheduleExecuted",
    AccountAlias = "Trading",
    Data = new
    {
        ScheduleId = "schedule-123",
        Ticker = "AAPL",
        Action = "Buy",
        Quantity = 10
    }
};

var json = JsonSerializer.Serialize(message);
await valkey.GetDatabase().PublishAsync(
    RedisChannel.Literal("notifications:schedules"), 
    json
);
```

### Critical Error 직접 전송

```csharp
// Program.cs에서 Valkey 연결 실패 시
try
{
    var valkey = services.GetRequiredService<IConnectionMultiplexer>();
    await valkey.GetDatabase().PingAsync();
}
catch (Exception ex)
{
    var systemLogger = services.GetRequiredService<ISystemLogger>();
    await systemLogger.LogSystemErrorAsync("Valkey Connection Failed", ex);
    throw; // 애플리케이션 종료
}
```

## 운영 가이드

### 알림 비활성화
특정 메시지 타입의 알림을 끄려면 `Targets`를 빈 배열로 설정:

```json
{
  "MessageRouting": {
    "System": {
      "Targets": []  // System 메시지 알림 비활성화
    }
  }
}
```

### Telegram 활성화
Telegram 알림을 추가하려면:

```json
{
  "MessageRouting": {
    "CriticalErrors": {
      "Targets": ["Discord", "Telegram"]  // 둘 다 전송
    }
  },
  "Telegram": {
    "Enabled": true
  }
}
```

### Rate Limit 조정
Discord rate limit에 걸리는 경우:

```json
{
  "Discord": {
    "RateLimitPerMinute": 20  // 기본 30에서 20으로 감소
  }
}
```

## 모니터링

Notifier 자체의 상태는 `/health` 엔드포인트로 확인:

```bash
curl http://localhost:5005/health
```

응답:
```json
{
  "status": "Healthy",
  "valkey": "Connected",
  "subscribedChannels": 4,
  "queueSize": 0,
  "timestamp": "2025-12-19T10:10:00Z"
}
```

## 문제 해결

### 메시지가 전송되지 않는 경우

1. **Notifier 로그 확인**
   ```bash
   journalctl -u yquant-notifier -f
   ```

2. **Valkey 채널 구독 확인**
   ```bash
   valkey-cli
   > PUBSUB CHANNELS notifications:*
   ```

3. **Discord Webhook 테스트**
   ```bash
   curl -X POST "https://discord.com/api/webhooks/..." \
     -H "Content-Type: application/json" \
     -d '{"content": "Test message"}'
   ```

### Rate Limit 초과

Discord에서 429 에러가 발생하는 경우:
- `RateLimitPerMinute` 값을 낮춤
- `BatchDelayMs` 값을 증가시켜 전송 속도 조절

### 메모리 부족

큐가 계속 쌓이는 경우:
- `MaxQueueSize` 확인
- Notifier 재시작
- Discord/Telegram API 상태 확인
