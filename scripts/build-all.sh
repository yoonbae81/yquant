#!/bin/bash
set -e

echo "🔨 Building all yQuant applications..."

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
        echo "💡 Please run: sudo mkdir -p $DEPLOY_ROOT && sudo chown -R \$USER:\$USER $DEPLOY_ROOT"
        exit 1
    }
fi

if [ ! -w "$DEPLOY_ROOT" ]; then
    echo "❌ Error: No write permission to $DEPLOY_ROOT"
    echo "💡 Please run: sudo chown -R \$USER:\$USER $DEPLOY_ROOT"
    exit 1
fi

echo "📦 Publishing BrokerGateway..."
dotnet publish src/03.Applications/yQuant.App.BrokerGateway/yQuant.App.BrokerGateway.csproj \
  -c Release \
  -o "$DEPLOY_ROOT/brokergateway"

echo "📦 Publishing OrderManager..."
dotnet publish src/03.Applications/yQuant.App.OrderManager/yQuant.App.OrderManager.csproj \
  -c Release \
  -o "$DEPLOY_ROOT/ordermanager"

echo "📦 Publishing Notifier..."
dotnet publish src/03.Applications/yQuant.App.Notifier/yQuant.App.Notifier.csproj \
  -c Release \
  -o "$DEPLOY_ROOT/notifier"

echo "📦 Publishing Console..."
dotnet publish src/03.Applications/yQuant.App.Console/yQuant.App.Console.csproj \
  -c Release \
  -o "$DEPLOY_ROOT/console"

echo "📦 Publishing Web..."
dotnet publish src/03.Applications/yQuant.App.Web/yQuant.App.Web.csproj \
  -c Release \
  -o "$DEPLOY_ROOT/web"

echo "📦 Publishing Webhook..."
dotnet publish src/03.Applications/yQuant.App.Webhook/yQuant.App.Webhook.csproj \
  -c Release \
  -o "$DEPLOY_ROOT/webhook"

echo "✅ All applications built successfully!"
