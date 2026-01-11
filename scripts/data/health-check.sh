#!/bin/bash
# scripts/data/health-check.sh
set -e

echo "🏥 Checking health of yQuant GATEWAY services..."

if systemctl --user is-active --quiet "console-sync.timer"; then
    echo "✅ console-sync.timer is active"
else
    echo "❌ console-sync.timer is NOT active"
    exit 1
fi

# Valkey Check (Optional but recommended for gateway 노드 as it hosts storage valkey)
echo "🔍 Checking Valkey status..."
if command -v valkey-cli &> /dev/null; then
    if valkey-cli ping | grep -q PONG; then
        echo "✅ Valkey is responsive"
    else
        echo "❌ Valkey is NOT responsive"
        exit 1
    fi
fi

echo "✅ Gateway services are healthy!"
