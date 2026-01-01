#!/bin/bash
# scripts/deploy-engine.sh
set -e

echo "🚀 Deploying yQuant Engine..."

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$PROJECT_ROOT"

echo "🔨 Building Engine..."
bash "$PROJECT_ROOT/scripts/build-engine.sh"

echo "🔄 Restarting Engine Services..."
bash "$PROJECT_ROOT/scripts/restart-engine.sh"

echo "✅ Engine deployment completed!"
