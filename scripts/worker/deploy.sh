#!/bin/bash
# scripts/worker/deploy.sh
set -e

SERVICE=$1
echo "🚀 Deploying yQuant to WORKER (Target: ${SERVICE:-all})..."

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "🔨 Building application..."
bash "$SCRIPT_DIR/build.sh" "$SERVICE"

echo "🔄 Restarting service..."
bash "$SCRIPT_DIR/restart.sh" "$SERVICE"

echo "✅ Worker deployment completed!"
