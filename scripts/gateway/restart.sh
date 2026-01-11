#!/bin/bash
# scripts/gateway/restart.sh
set -e

echo "🔄 Restarting yQuant GATEWAY services (Catalog Sync)..."

# Restart Timer
echo "🔄 Restarting console-sync.timer..."
systemctl --user restart console-sync.timer

if systemctl --user is-active --quiet console-sync.timer; then
    echo "✅ console-sync.timer is active"
else
    echo "❌ console-sync.timer failed to start"
    exit 1
fi

echo "✅ Gateway restart process completed!"
