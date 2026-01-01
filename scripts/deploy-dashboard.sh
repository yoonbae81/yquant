#!/bin/bash
# scripts/deploy-dashboard.sh
set -e

echo "🚀 Deploying yQuant Dashboard..."

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$PROJECT_ROOT"

echo "🔨 Building Dashboard..."
bash "$PROJECT_ROOT/scripts/build-dashboard.sh"

echo "🔄 Restarting Dashboard Service..."
bash "$PROJECT_ROOT/scripts/restart-dashboard.sh"

echo "✅ Dashboard deployment completed!"
