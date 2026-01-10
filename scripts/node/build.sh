#!/bin/bash
# scripts/node/build.sh
set -e

SERVICE=$1
echo "🔨 Building yQuant NODE applications (Target: ${SERVICE:-all})..."

# 프로젝트 루트 디렉토리
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$PROJECT_ROOT"

# 배포 대상 디렉토리
DEPLOY_ROOT="${DEPLOY_ROOT:-/srv/yquant}"

# 디렉토리 권한 설정 (최초 1회 필요)
if [ ! -d "$DEPLOY_ROOT" ]; then
    echo "📁 Creating deployment directory: $DEPLOY_ROOT"
    sudo mkdir -p "$DEPLOY_ROOT"
fi
echo "🔑 Setting permissions for $DEPLOY_ROOT..."
sudo chown -R $USER "$DEPLOY_ROOT"

# 서비스 빌드 함수
build_service() {
    local name=$1
    local project_path=$2
    echo "📦 Publishing $name..."
    dotnet publish "$project_path" -c Release -o "$DEPLOY_ROOT/$name"
}

case "$SERVICE" in
    "brokergateway")
        build_service "brokergateway" "src/03.Applications/yQuant.App.BrokerGateway/yQuant.App.BrokerGateway.csproj"
        ;;
    "ordermanager")
        build_service "ordermanager" "src/03.Applications/yQuant.App.OrderManager/yQuant.App.OrderManager.csproj"
        ;;
    "notifier")
        build_service "notifier" "src/03.Applications/yQuant.App.Notifier/yQuant.App.Notifier.csproj"
        ;;
    "webhook")
        build_service "webhook" "src/03.Applications/yQuant.App.Webhook/yQuant.App.Webhook.csproj"
        ;;
    "dashboard")
        build_service "dashboard" "src/03.Applications/yQuant.App.Dashboard/yQuant.App.Dashboard.csproj"
        ;;
    "")
        # Build all
        build_service "brokergateway" "src/03.Applications/yQuant.App.BrokerGateway/yQuant.App.BrokerGateway.csproj"
        build_service "ordermanager" "src/03.Applications/yQuant.App.OrderManager/yQuant.App.OrderManager.csproj"
        build_service "notifier" "src/03.Applications/yQuant.App.Notifier/yQuant.App.Notifier.csproj"
        build_service "webhook" "src/03.Applications/yQuant.App.Webhook/yQuant.App.Webhook.csproj"
        build_service "dashboard" "src/03.Applications/yQuant.App.Dashboard/yQuant.App.Dashboard.csproj"
        ;;
    *)
        echo "❌ Unknown service: $SERVICE"
        exit 1
        ;;
esac

echo "✅ Node build process completed!"
