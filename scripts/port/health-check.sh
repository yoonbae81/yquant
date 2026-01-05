#!/bin/bash
# scripts/port/health-check.sh
set -e

echo "🏥 Checking health of yQuant PORT services..."

if systemctl --user is-active --quiet "console-sync.timer"; then
    echo "✅ console-sync.timer is active"
else
    echo "❌ console-sync.timer is NOT active"
    exit 1
fi

# Valkey & Sentinel Check (Optional but recommended for port node as it hosts storage valkey)
echo "🔍 Checking Valkey status..."
if command -v valkey-cli &> /dev/null; then
    if valkey-cli ping | grep -q PONG; then
        echo "✅ Valkey is responsive"
    else
        echo "❌ Valkey is NOT responsive"
        exit 1
    fi
fi

echo "✅ Port services are healthy!"
