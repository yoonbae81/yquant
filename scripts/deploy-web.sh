#!/bin/bash
# scripts/deploy-web.sh
set -e

echo "🚀 Deploying yQuant Web..."

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$PROJECT_ROOT"

echo "🔨 Building Web..."
bash "$PROJECT_ROOT/scripts/build-web.sh"

echo "🔄 Restarting Web Service..."
bash "$PROJECT_ROOT/scripts/restart-web.sh"

echo "✅ Web deployment completed!"
