# 데모 모드 가이드

yQuant는 실제 자산이나 KIS 계좌 없이도 시스템의 전체 기능(대시보드, 주문 관리, 전략 실행 등)을 테스트할 수 있는 **데모 모드**를 지원합니다.

데모 모드는 Yahoo Finance로부터 실시간 시장 데이터를 가져오고 메모리에서 가상 잔고 및 포지션을 관리하는 **Mock KIS 서버**를 사용합니다.

---

## 1. 사전 요구 사항

- **Docker & Docker Compose**: 데모 인프라(Redis, KIS-Mock)를 실행하는 데 필요합니다.
- **Python 3.9+**: Docker 없이 Mock 서버를 직접 실행하려는 경우 필요합니다.
- **.NET 10.0 SDK**: yQuant 애플리케이션을 실행하는 데 필요합니다.

---

## 2. 인프라 설정

데모 환경은 운영 데이터와의 간섭을 방지하기 위해 전용 Redis 인스턴스와 KIS Mock 서버가 필요합니다.

1. `demo` 디렉토리로 이동합니다:
   ```bash
   cd demo
   ```
2. 인프라를 시작합니다:
   ```bash
   docker-compose up -d
   ```
   이 명령어는 다음 서비스를 시작합니다:
   - **Redis (Message)**: 6380 포트 (이벤트 및 상태 관리용)
   - **Redis (Token)**: 6381 포트 (금융사 인증 토큰 저장용)
   - **KIS Mock**: 9443 포트 (API 엔드포인트)

---

## 3. 데모 모드를 위한 yQuant 설정

yQuant 애플리케이션이 데모 인프라를 바라보도록 설정해야 합니다.

### `appsettings.json` (루트 디렉토리)

다음 설정값들을 수정합니다:

```json
{
  "Redis": {
    "Message": "localhost:6380",
    "Token": "localhost:6381"
  },
  "BrokerGateway": {
    "KIS": {
      "BaseUrl": "http://localhost:9443"
    }
  }
}
```

> **참고**: 모든 서비스를 컨테이너 네트워크 내부에서 실행하는 환경(예: 공용 서버 배포)에서는 `localhost` 대신 Docker 서비스 이름을 사용해야 합니다.

---

## 4. 데모 실행 순서

1. **국내/해외 종목 카탈로그 동기화**:
   데모는 전용 Redis를 사용하므로, 먼저 종목 메타데이터를 채워야 합니다.
   ```bash
   # 루트 디렉토리에서 실행
   dotnet run --project src/03.Applications/yQuant.App.Console -- catalog --kr --us
   ```

2. **서비스 시작**:
   다음 순서대로 핵심 서비스들을 실행합니다:
   - `BrokerGateway`: 가상 잔고 동기화 및 Mock 주문 처리 담당
   - `OrderManager`: 전략 및 스케줄 모니터링 담당
   - `Web Dashboard`: 사용자 인터페이스(UI) 제공

   ```bash
   dotnet run --project src/03.Applications/yQuant.App.BrokerGateway
   dotnet run --project src/03.Applications/yQuant.App.OrderManager
   dotnet run --project src/03.Applications/yQuant.App.Web
   ```

3. **대시보드 접속**:
   브라우저에서 `http://localhost:5000` (또는 설정된 포트)에 접속합니다.

---

## 5. Mock 서버 작동 원리

- **시장 데이터**: `yfinance`를 사용하여 KIS 종목 코드(예: `005930` -> `005930.KS`)에 대한 실제 가격을 가져옵니다.
- **가상 잔고**: 모든 계좌는 **1억 원(KRW)**의 가상 예수금으로 시작합니다.
- **주문 체결**: 주문은 현재 시장가로 즉시 처리됩니다.
- **데이터 보존**: Mock 서버는 데이터를 메모리에 저장합니다. `kis-mock` 컨테이너를 재시작하면 잔고와 포지션이 초기화됩니다.
- **보안**: 실제 `AppKey`나 `Secret`이 필요하지 않습니다. `BrokerGateway`는 Mock 서버와 통신하며, Mock 서버는 인증 정보를 무시합니다.

---

## 6. 환경 정리

데모 인프라를 정지하고 삭제하려면 다음 명령어를 실행합니다:
```bash
cd demo
docker-compose down
```
