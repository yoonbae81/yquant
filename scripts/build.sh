#!/bin/bash
# scripts/build.sh
set -e

TYPE=$1
echo "🔨 Building yQuant applications (Target: ${TYPE:-all})..."

# 프로젝트 루트 디렉토리
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$PROJECT_ROOT"

# 배포 대상 디렉토리
DEPLOY_ROOT="${DEPLOY_ROOT:-/srv/yquant}"

# 권한 확인 및 디렉토리 생성 시도 (필요시 sudo 사용)
if [ ! -d "$DEPLOY_ROOT" ]; then
    echo "📂 Creating deploy directory with sudo: $DEPLOY_ROOT"
    sudo mkdir -p "$DEPLOY_ROOT"
    sudo chown -R $(id -u):$(id -g) "$DEPLOY_ROOT"
fi

# Build logic based on type
SHOULD_BUILD_NODE=true
SHOULD_BUILD_PORT=true

if [ "$TYPE" == "port" ]; then
    SHOULD_BUILD_NODE=false
elif [ "$TYPE" == "node" ]; then
    SHOULD_BUILD_PORT=false
fi

if [ "$SHOULD_BUILD_NODE" = true ]; then
    echo "📦 Publishing BrokerGateway..."
    dotnet publish src/03.Applications/yQuant.App.BrokerGateway/yQuant.App.BrokerGateway.csproj \
      -c Release -o "$DEPLOY_ROOT/brokergateway"

    echo "📦 Publishing OrderManager..."
    dotnet publish src/03.Applications/yQuant.App.OrderManager/yQuant.App.OrderManager.csproj \
      -c Release -o "$DEPLOY_ROOT/ordermanager"

    echo "📦 Publishing Notifier..."
    dotnet publish src/03.Applications/yQuant.App.Notifier/yQuant.App.Notifier.csproj \
      -c Release -o "$DEPLOY_ROOT/notifier"

    echo "📦 Publishing Webhook..."
    dotnet publish src/03.Applications/yQuant.App.Webhook/yQuant.App.Webhook.csproj \
      -c Release -o "$DEPLOY_ROOT/webhook"

    echo "📦 Publishing Dashboard..."
    dotnet publish src/03.Applications/yQuant.App.Dashboard/yQuant.App.Dashboard.csproj \
      -c Release -o "$DEPLOY_ROOT/dashboard"
fi

if [ "$SHOULD_BUILD_PORT" = true ]; then
    echo "📦 Publishing Console (Catalog Sync Tool)..."
    dotnet publish src/03.Applications/yQuant.App.Console/yQuant.App.Console.csproj \
      -c Release -o "$DEPLOY_ROOT/console"
fi

echo "✅ Build process completed!"
