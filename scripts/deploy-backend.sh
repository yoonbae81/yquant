#!/bin/bash
# scripts/deploy-backend.sh
set -e

echo "🚀 Deploying yQuant Backend..."

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$PROJECT_ROOT"

echo "🔨 Building Backend..."
bash "$PROJECT_ROOT/scripts/build-backend.sh"

echo "🔄 Restarting Backend Services..."
bash "$PROJECT_ROOT/scripts/restart-backend.sh"

echo "✅ Backend deployment completed!"
