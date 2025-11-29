# KIS 계정 연결 테스트 도구

`tools/KISTest`는 KIS Account:0 계정의 연결 상태를 테스트하는 유틸리티입니다.

## 특징

- **BrokerGateway와 동일한 User Secrets 사용**
- 별도의 credential 복사 불필요
- Account:0 계정의 인증 및 잔고 조회 테스트

## 사용 방법

### 기본 테스트
```powershell
cd tools\KISTest
dotnet run
```

### 토큰 강제 갱신
기존 캐시된 토큰을 폐기하고 새로운 토큰을 발급받으려면:
```powershell
cd tools\KISTest
dotnet run --refresh-token
# 또는
dotnet run -r
```

## 테스트 항목

1. **KIS Client 생성** - KISClient 인스턴스 생성 확인
2. **API 연결** - Access Token 획득 및 인증
3. **계좌 잔고 조회** - 국내/해외 계좌 잔고 확인

## 사전 요구사항

BrokerGateway의 User Secrets에 Account:0 설정이 필요합니다:

```powershell
cd src\03.Applications\yQuant.App.BrokerGateway
dotnet user-secrets set "Accounts:0:UserId" "YOUR_USER_ID"
dotnet user-secrets set "Accounts:0:AccountNumber" "YOUR_ACCOUNT_NUMBER"
dotnet user-secrets set "Accounts:0:Credentials:AppKey" "YOUR_APP_KEY"
dotnet user-secrets set "Accounts:0:Credentials:AppSecret" "YOUR_APP_SECRET"
dotnet user-secrets set "Accounts:0:Credentials:BaseUrl" "https://openapi.koreainvestment.com:9443"
```

## 출력 예시

```
🔍 KIS Account Connection Test
================================

📋 Account Info:
   Alias: MainAccount
   UserId: yxbae81
   AccountNumber: 64664736-01
   BrokerType: KIS
   BaseUrl: https://openapi.koreainvestment.com:9443

Test 1: Creating KIS Client...
✅ KIS Client created

Test 2: Connecting to KIS API (getting access token)...
✅ Successfully connected to KIS API!

Test 3: Getting account balance...
✅ Account retrieved:
   Account ID: 64664736-01
   Broker: KIS
   Active: True
   Deposits: 2 currencies

   KRW: 1,234,567
   USD: 5,678

================================
✅ All tests passed!
KIS Account 'MainAccount' is ready!
```
