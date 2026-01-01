#!/bin/bash
# scripts/setup-engine.sh
set -e

echo "⚙️ Setting up yQuant Engine services (systemd)..."

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SYSTEMD_DIR="$HOME/.config/systemd/user"
TEMPLATE_DIR="$PROJECT_ROOT/scripts/systemd"

mkdir -p "$SYSTEMD_DIR"

SERVICES=(
  "brokergateway.service"
  "ordermanager.service"
  "notifier.service"
  "webhook.service"
  "console-sync.service"
)

for service in "${SERVICES[@]}"; do
  echo "  → Installing $service"
  cp "$TEMPLATE_DIR/$service" "$SYSTEMD_DIR/$service"
done

echo "  → Installing console-sync.timer"
cp "$TEMPLATE_DIR/console-sync.timer" "$SYSTEMD_DIR/console-sync.timer"

systemctl --user daemon-reload

echo "✅ Engine services installed!"
echo "💡 To enable: systemctl --user enable brokergateway ordermanager notifier webhook console-sync.timer"
echo "💡 To start:  systemctl --user start brokergateway ordermanager notifier webhook console-sync.timer"
