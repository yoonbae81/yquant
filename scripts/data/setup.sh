#!/bin/bash
# scripts/data/setup.sh
set -e

echo "⚙️ Setting up yQuant services (Catalog Sync)..."

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SYSTEMD_DIR="$HOME/.config/systemd/user"
TEMPLATE_DIR="$PROJECT_ROOT/scripts/systemd"

mkdir -p "$SYSTEMD_DIR"

# Install Console Sync Service & Timer
if [ -f "$TEMPLATE_DIR/console-sync.service" ]; then
    echo "  → Installing console-sync.service"
    cp "$TEMPLATE_DIR/console-sync.service" "$SYSTEMD_DIR/console-sync.service"
fi

if [ -f "$TEMPLATE_DIR/console-sync.timer" ]; then
    echo "  → Installing console-sync.timer"
    cp "$TEMPLATE_DIR/console-sync.timer" "$SYSTEMD_DIR/console-sync.timer"
fi

systemctl --user daemon-reload

echo "✅ Gateway setup process completed!"
echo "💡 To enable: systemctl --user enable console-sync.timer"
echo "💡 To start:  systemctl --user start console-sync.timer"
