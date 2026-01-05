#!/bin/bash
# scripts/port/deploy.sh
set -e

echo "🚀 Deploying yQuant to PORT node..."

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "🔨 Building application..."
bash "$SCRIPT_DIR/build.sh"

echo "🔄 Restarting service..."
bash "$SCRIPT_DIR/restart.sh"

echo "✅ Port deployment completed!"
