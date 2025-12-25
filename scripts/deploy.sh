#!/bin/bash
set -e

echo "🚀 Starting yQuant deployment..."

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$PROJECT_ROOT"

echo "📥 Pulling latest code..."
# git pull is now handled by the CI/CD workflow

echo "🔨 Building applications..."
bash "$PROJECT_ROOT/scripts/build-all.sh"

echo "🔄 Restarting services..."
bash "$PROJECT_ROOT/scripts/restart-services.sh"

echo "✅ Deployment completed successfully!"
echo ""
echo "📊 Service status:"
systemctl --user status brokergateway ordermanager notifier web webhook --no-pager | grep -E "(●|Active:)"
