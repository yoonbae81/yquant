#!/bin/bash
# scripts/data/build.sh
set -e

echo "🔨 Building yQuant GATEWAY applications (Console Sync)..."

# 프로젝트 루트 디렉토리 (scripts/data 서브디렉토리 기준)
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$PROJECT_ROOT"

# 배포 대상 디렉토리
DEPLOY_ROOT="${DEPLOY_ROOT:-/srv/yquant}"

# 권한 확인 및 디렉토리 생성 시도
if [ ! -d "$DEPLOY_ROOT" ]; then
    echo "📂 Creating deploy directory: $DEPLOY_ROOT"
    sudo mkdir -p "$DEPLOY_ROOT"
    sudo chown -R $(id -u):$(id -g) "$DEPLOY_ROOT"
fi

echo "📦 Publishing Console (Catalog Sync Tool)..."
dotnet publish src/03.Applications/yQuant.App.Console/yQuant.App.Console.csproj \
  -c Release -o "$DEPLOY_ROOT/console"

echo "✅ Gateway build process completed!"
