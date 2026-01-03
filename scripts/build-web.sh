#!/bin/bash
# scripts/build-web.sh
set -e

echo "🔨 Building yQuant Web application..."

# 프로젝트 루트 디렉토리
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$PROJECT_ROOT"

# 배포 대상 디렉토리
DEPLOY_ROOT="${DEPLOY_ROOT:-/srv/yquant}"

# 권한 확인 및 디렉토리 생성 시도
if [ ! -d "$DEPLOY_ROOT" ]; then
    echo "📂 Creating deploy directory: $DEPLOY_ROOT"
    mkdir -p "$DEPLOY_ROOT" || {
        echo "❌ Error: Cannot create directory $DEPLOY_ROOT"
        exit 1
    }
fi

echo "📦 Publishing Web..."
dotnet publish src/03.Applications/yQuant.App.Web/yQuant.App.Web.csproj \
  -c Release \
  -o "$DEPLOY_ROOT/web"

echo "✅ Web application built successfully!"
