# Redis 연결 테스트 도구

`tools/RedisTest` 디렉토리에 Redis 연결 테스트 유틸리티가 생성되었습니다.

## 사용 방법

```powershell
cd tools\RedisTest
dotnet run
```

## 테스트 항목

1. **연결 테스트** - Redis 서버 연결 확인
2. **Ping 테스트** - 응답 시간 측정
3. **읽기/쓰기 테스트** - 데이터 저장 및 조회
4. **Pub/Sub 테스트** - 메시지 발행/구독 기능
5. **서버 정보** - Redis 서버 엔드포인트 확인

## 간단한 테스트 방법

Redis가 실행 중인지 확인:
```powershell
docker ps | findstr redis
```

Redis CLI로 직접 테스트:
```powershell
docker exec -it <container_id> redis-cli PING
```

응답이 `PONG`이면 정상 작동 중입니다.
