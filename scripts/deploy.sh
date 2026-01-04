#!/bin/bash
# scripts/deploy.sh
set -e

echo "🚀 Deploying yQuant to the current node..."

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$PROJECT_ROOT"

echo "🔨 Building applications..."
bash "$PROJECT_ROOT/scripts/build.sh"

echo "🔄 Restarting services..."
bash "$PROJECT_ROOT/scripts/restart.sh"

echo "✅ Deployment completed on this node!"
