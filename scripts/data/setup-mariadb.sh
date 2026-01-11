#!/bin/bash
# scripts/data/setup-mariadb.sh
# MariaDB 데이터베이스 및 사용자 설정 스크립트

set -e

echo "🗄️  Setting up MariaDB for yQuant..."

# 비밀번호 입력 받기
read -sp "Enter password for yQuant MariaDB user: " DB_PASSWORD
echo

if [ -z "$DB_PASSWORD" ]; then
    echo "❌ Password cannot be empty"
    exit 1
fi

# MariaDB가 설치되어 있는지 확인
if ! command -v mysql &> /dev/null; then
    echo "❌ MariaDB/MySQL client not found. Please install MariaDB first:"
    echo "   sudo dnf install mariadb-server"
    exit 1
fi

# MariaDB가 실행 중인지 확인
if ! systemctl is-active --quiet mariadb; then
    echo "⚠️  MariaDB is not running. Starting MariaDB..."
    sudo systemctl start mariadb
fi

echo "📝 Creating database and user..."

# SQL 명령 실행
sudo mysql -e "
CREATE DATABASE IF NOT EXISTS yquant CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS 'yquant'@'%' IDENTIFIED BY '$DB_PASSWORD';
GRANT ALL PRIVILEGES ON yquant.* TO 'yquant'@'%';
FLUSH PRIVILEGES;
"

if [ $? -eq 0 ]; then
    echo "✅ MariaDB setup completed successfully!"
    echo ""
    echo "📋 Database Information:"
    echo "   Database: yquant"
    echo "   User: yquant"
    echo "   Host: % (all hosts)"
    echo ""
    echo "🔧 Update your appsecrets.json with:"
    echo "   \"MariaDB\": \"Server=localhost;Port=3306;Database=yquant;User=yquant;Password=$DB_PASSWORD;CharSet=utf8mb4\""
    echo ""
    echo "💡 For remote access, update the connection string Server to the actual hostname"
else
    echo "❌ Failed to setup MariaDB"
    exit 1
fi
