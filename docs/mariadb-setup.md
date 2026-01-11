# MariaDB 설정 가이드

yQuant는 영구 데이터 저장을 위해 MariaDB를 사용합니다. 이 문서는 MariaDB 설치 및 설정 방법을 안내합니다.

## 1. MariaDB 설치 (Oracle Linux 10)

```bash
# MariaDB 서버 설치
sudo dnf install mariadb-server

# MariaDB 서비스 시작 및 자동 시작 설정
sudo systemctl start mariadb
sudo systemctl enable mariadb

# 초기 보안 설정 (선택사항이지만 권장)
sudo mysql_secure_installation
```

`mysql_secure_installation` 실행 시:
- root 비밀번호 설정
- 익명 사용자 제거
- 원격 root 로그인 비활성화
- 테스트 데이터베이스 제거

## 2. 데이터베이스 및 사용자 생성

### 방법 1: 자동 스크립트 사용 (권장)

```bash
cd /path/to/yQuant
bash scripts/gateway/setup-mariadb.sh
```

스크립트 실행 시 비밀번호를 입력하라는 프롬프트가 표시됩니다.

### 방법 2: 수동 설정

```bash
# MariaDB 접속
sudo mysql

# 또는 root 비밀번호를 설정한 경우
mysql -u root -p
```

다음 SQL 명령을 실행:

```sql
-- 데이터베이스 생성
CREATE DATABASE yquant CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- 사용자 생성 (비밀번호는 강력한 것으로 변경)
CREATE USER 'yquant'@'%' IDENTIFIED BY 'your_secure_password_here';

-- 권한 부여
GRANT ALL PRIVILEGES ON yquant.* TO 'yquant'@'%';

-- 권한 적용
FLUSH PRIVILEGES;

-- 종료
EXIT;
```

## 3. 연결 문자열 설정

`appsecrets.json` 파일의 `ConnectionStrings` 섹션을 업데이트:

```json
{
  "ConnectionStrings": {
    "Valkey": "localhost:6379",
    "MariaDB": "Server=localhost;Port=3306;Database=yquant;User=yquant;Password=your_secure_password_here;CharSet=utf8mb4"
  }
}
```

### 원격 접속 설정

다른 서버에서 MariaDB에 접속해야 하는 경우:

1. **연결 문자열 업데이트**:
   ```json
   "MariaDB": "Server=yq-gateway;Port=3306;Database=yquant;User=yquant;Password=your_password;CharSet=utf8mb4"
   ```

2. **MariaDB 방화벽 설정**:
   ```bash
   # 포트 3306 열기
   sudo firewall-cmd --permanent --add-port=3306/tcp
   sudo firewall-cmd --reload
   ```

3. **MariaDB 바인드 주소 설정** (`/etc/my.cnf.d/mariadb-server.cnf`):
   ```ini
   [mysqld]
   bind-address = 0.0.0.0
   ```
   
   설정 후 재시작:
   ```bash
   sudo systemctl restart mariadb
   ```

## 4. 스키마 초기화

애플리케이션 첫 실행 시 Entity Framework Core가 자동으로 테이블을 생성합니다:

```bash
cd src/03.Applications/yQuant.App.BrokerGateway
dotnet run
```

또는 수동으로 확인:

```bash
mysql -u yquant -p yquant

# 테이블 목록 확인
SHOW TABLES;

# 예상 테이블:
# - trades
# - catalog
# - catalog_metadata
# - tokens
# - scheduled_orders
# - daily_snapshots
```

## 5. 백업 및 복원

### 백업

```bash
# 전체 데이터베이스 백업
mysqldump -u yquant -p yquant > yquant_backup_$(date +%Y%m%d).sql

# 압축 백업
mysqldump -u yquant -p yquant | gzip > yquant_backup_$(date +%Y%m%d).sql.gz
```

### 복원

```bash
# 백업 복원
mysql -u yquant -p yquant < yquant_backup_20260111.sql

# 압축 파일 복원
gunzip < yquant_backup_20260111.sql.gz | mysql -u yquant -p yquant
```

## 6. 문제 해결

### 연결 오류

```bash
# MariaDB 상태 확인
sudo systemctl status mariadb

# 로그 확인
sudo journalctl -u mariadb -n 50

# 연결 테스트
mysql -u yquant -p -h localhost yquant
```

### 권한 문제

```sql
-- 사용자 권한 확인
SHOW GRANTS FOR 'yquant'@'%';

-- 권한 재부여
GRANT ALL PRIVILEGES ON yquant.* TO 'yquant'@'%';
FLUSH PRIVILEGES;
```

### 성능 모니터링

```sql
-- 현재 연결 확인
SHOW PROCESSLIST;

-- 데이터베이스 크기 확인
SELECT 
    table_schema AS 'Database',
    ROUND(SUM(data_length + index_length) / 1024 / 1024, 2) AS 'Size (MB)'
FROM information_schema.tables
WHERE table_schema = 'yquant'
GROUP BY table_schema;
```

## 7. 보안 권장사항

1. **강력한 비밀번호 사용**: 최소 16자 이상, 영문/숫자/특수문자 조합
2. **최소 권한 원칙**: 필요한 권한만 부여
3. **정기적인 백업**: 매일 자동 백업 설정 권장
4. **방화벽 설정**: 필요한 IP만 접근 허용
5. **SSL/TLS 연결**: 프로덕션 환경에서는 암호화된 연결 사용 권장

## 참고

- MariaDB 공식 문서: https://mariadb.com/kb/en/documentation/
- Entity Framework Core: https://learn.microsoft.com/ef/core/
