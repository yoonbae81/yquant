# yQuant Web Dashboard 사용자 인증

## 개요

yQuant.App.Web은 간단한 Cookie 기반 인증을 사용하여 대시보드 접근을 제어합니다.

## 사용자 관리

### 1. 사용자 정보 파일 위치

```
src/03.Applications/yQuant.App.Web/users.json
```

⚠️ **보안 주의**: 이 파일은 Git에 커밋되지 않습니다 (`.gitignore`에 포함됨)

### 2. 파일 구조

```json
{
  "Users": [
    {
      "Username": "admin",
      "PasswordHash": "$2a$11$...",
      "Role": "Admin"
    },
    {
      "Username": "viewer",
      "PasswordHash": "$2a$11$...",
      "Role": "Viewer"
    }
  ],
  "SessionTimeout": 480
}
```

**필드 설명**:
- `Username`: 로그인 시 사용할 사용자 이름
- `PasswordHash`: BCrypt로 해시된 비밀번호 (평문 저장 금지!)
- `Role`: 사용자 역할 (현재는 미사용, 향후 확장용)
- `SessionTimeout`: 세션 유지 시간 (분 단위, 기본 480분 = 8시간)

## 비밀번호 해시 생성 방법

### 방법 1: 온라인 BCrypt 생성기 (권장)

가장 빠르고 간단한 방법입니다.

1. 웹 브라우저에서 다음 사이트 중 하나를 방문:
   - https://bcrypt-generator.com/
   - https://www.browserling.com/tools/bcrypt

2. 원하는 비밀번호 입력

3. **Rounds** 설정: `11` (기본값, 보안과 성능의 균형)

4. **Generate Hash** 버튼 클릭

5. 생성된 해시값을 복사하여 `users.json`의 `PasswordHash` 필드에 붙여넣기

**예시**:
```
비밀번호: mySecurePassword123
생성된 해시: $2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy
```

### 방법 2: .NET Interactive (C# 스크립트)

개발 환경에서 직접 생성하는 방법입니다.

#### 2-1. 필요한 패키지 설치

```bash
dotnet tool install -g dotnet-script
```

#### 2-2. 해시 생성 스크립트 실행

**파일 생성**: `generate_hash.csx`
```csharp
#!/usr/bin/env dotnet-script
#r "nuget: BCrypt.Net-Next, 4.0.3"

using BCrypt.Net;

if (Args.Count == 0)
{
    Console.WriteLine("Usage: dotnet script generate_hash.csx <password>");
    return;
}

var password = Args[0];
var hash = BCrypt.HashPassword(password, 11);

Console.WriteLine($"\nPassword: {password}");
Console.WriteLine($"Hash:     {hash}");
Console.WriteLine($"\nCopy this hash to users.json:");
Console.WriteLine($"\"PasswordHash\": \"{hash}\"");
```

**실행**:
```bash
dotnet script generate_hash.csx "mySecurePassword123"
```

**출력**:
```
Password: mySecurePassword123
Hash:     $2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy

Copy this hash to users.json:
"PasswordHash": "$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy"
```

### 방법 3: Python (시스템에 Python이 설치된 경우)

```bash
# bcrypt 패키지 설치
pip install bcrypt

# Python 인터랙티브 모드 실행
python3
```

```python
import bcrypt

password = b"mySecurePassword123"
hash = bcrypt.hashpw(password, bcrypt.gensalt(rounds=11))
print(hash.decode('utf-8'))
```

### 방법 4: Node.js (시스템에 Node.js가 설치된 경우)

```bash
# bcrypt 패키지 설치
npm install -g bcrypt

# Node.js 인터랙티브 모드 실행
node
```

```javascript
const bcrypt = require('bcrypt');
const password = 'mySecurePassword123';
const hash = bcrypt.hashSync(password, 11);
console.log(hash);
```

## 사용자 추가/수정

### 새 사용자 추가

1. `users.json` 파일 열기

2. `Users` 배열에 새 항목 추가:

```json
{
  "Users": [
    {
      "Username": "admin",
      "PasswordHash": "$2a$11$...",
      "Role": "Admin"
    },
    {
      "Username": "newuser",
      "PasswordHash": "$2a$11$...",
      "Role": "User"
    }
  ],
  "SessionTimeout": 480
}
```

3. 파일 저장

4. App.Web 재시작 (파일 변경 시 자동 리로드됨)

### 비밀번호 변경

1. 새 비밀번호의 해시 생성 (위 방법 중 하나 사용)

2. `users.json`에서 해당 사용자의 `PasswordHash` 값 교체

3. 파일 저장

### 사용자 삭제

1. `users.json`에서 해당 사용자 항목 제거

2. 파일 저장

## 초기 설정

### 1. 예시 파일 복사

```bash
cd src/03.Applications/yQuant.App.Web
cp users.example.json users.json
```

### 2. 기본 비밀번호 변경

**기본 계정 정보**:
- Username: `admin`
- Password: `admin123`

⚠️ **보안 경고**: 프로덕션 환경에서는 반드시 비밀번호를 변경하세요!

**변경 방법**:
1. 새 비밀번호의 해시 생성 (위 방법 참고)
2. `users.json`의 `PasswordHash` 값 교체
3. 파일 저장

### 3. 세션 타임아웃 조정 (선택사항)

```json
{
  "SessionTimeout": 480  // 분 단위 (기본 8시간)
}
```

**권장 값**:
- 개발 환경: `480` (8시간)
- 프로덕션 환경: `120` (2시간) ~ `240` (4시간)
- 높은 보안 요구: `60` (1시간)

## 보안 모범 사례

### 1. 강력한 비밀번호 사용

✅ **좋은 예**:
- `MyTr@d1ng$ystem2024!`
- `Quant#Secure99Pass`
- `yQ!2024$Str0ng`

❌ **나쁜 예**:
- `admin`
- `password123`
- `12345678`

**권장 사항**:
- 최소 12자 이상
- 대소문자, 숫자, 특수문자 혼합
- 사전에 있는 단어 사용 금지
- 개인 정보 (생일, 이름 등) 사용 금지

### 2. 정기적인 비밀번호 변경

- 3~6개월마다 비밀번호 변경 권장
- 보안 사고 의심 시 즉시 변경

### 3. users.json 파일 보호

```bash
# 파일 권한 설정 (Linux/macOS)
chmod 600 users.json  # 소유자만 읽기/쓰기 가능

# 파일 소유자 확인
ls -l users.json
```

### 4. HTTPS 사용 필수

Cookie 기반 인증은 HTTPS 없이는 안전하지 않습니다.

**개발 환경**:
```bash
dotnet dev-certs https --trust
```

**프로덕션 환경**:
- Let's Encrypt 인증서 사용
- 또는 리버스 프록시(Nginx)에서 SSL 처리

### 5. Git 커밋 방지

`.gitignore`에 다음 항목이 있는지 확인:

```gitignore
# User authentication data
users.json
```

**실수로 커밋한 경우**:
```bash
# Git 히스토리에서 완전히 제거
git filter-branch --force --index-filter \
  "git rm --cached --ignore-unmatch src/03.Applications/yQuant.App.Web/users.json" \
  --prune-empty --tag-name-filter cat -- --all

# 강제 푸시 (주의!)
git push origin --force --all
```

## 문제 해결

### 로그인 실패 ("Invalid username or password")

**원인 1**: 비밀번호 해시가 잘못됨
- 해결: 비밀번호 해시를 다시 생성하여 교체

**원인 2**: `users.json` 파일 형식 오류
- 해결: JSON 유효성 검사 (https://jsonlint.com/)

**원인 3**: 파일 권한 문제
- 해결: `chmod 644 users.json` (읽기 권한 부여)

### "users.json not found" 오류

**원인**: 파일이 없거나 경로가 잘못됨

**해결**:
```bash
cd src/03.Applications/yQuant.App.Web
cp users.example.json users.json
```

### 세션이 너무 빨리 만료됨

**원인**: `SessionTimeout` 값이 너무 작음

**해결**: `users.json`에서 `SessionTimeout` 값 증가
```json
{
  "SessionTimeout": 480  // 8시간으로 증가
}
```

### 파일 변경이 반영되지 않음

**원인**: 애플리케이션이 재시작되지 않음

**해결**:
```bash
# 개발 환경: Ctrl+C로 중지 후 재시작
dotnet run

# 프로덕션 환경 (systemd)
sudo systemctl restart yquant-web
```

## 향후 확장 계획

현재는 간단한 파일 기반 인증을 사용하지만, 사용자가 증가하면 다음으로 업그레이드할 수 있습니다:

### ASP.NET Core Identity (3~10명)
- SQLite 기반 사용자 DB
- 사용자 관리 UI 제공
- 역할 기반 접근 제어
- 비밀번호 재설정 기능

### 2FA (Two-Factor Authentication)
- TOTP (Google Authenticator, Authy 등)
- SMS 인증
- 이메일 인증

### 감사 로그
- 로그인 시도 기록
- 실패한 로그인 추적
- 사용자 활동 로그

## 참고 자료

- [BCrypt 공식 문서](https://github.com/BcryptNet/bcrypt.net)
- [ASP.NET Core Authentication](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/)
- [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)
