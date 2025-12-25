#!/bin/bash
set -e

echo "🔨 Building all yQuant applications..."

# 프로젝트 루트 디렉토리
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$PROJECT_ROOT"

# 배포 대상 디렉토리
DEPLOY_ROOT="${DEPLOY_ROOT:-/srv/yquant}"

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
