#!/bin/bash
# scripts/setup.sh
set -e

echo "⚙️ Setting up all yQuant services (systemd)..."

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SYSTEMD_DIR="$HOME/.config/systemd/user"
TEMPLATE_DIR="$PROJECT_ROOT/scripts/systemd"

mkdir -p "$SYSTEMD_DIR"

SERVICES=(
  "brokergateway.service"
  "ordermanager.service"
  "notifier.service"
  "webhook.service"
  "frontend.service"
  "console-sync.service"
)

for service in "${SERVICES[@]}"; do
  if [ -f "$TEMPLATE_DIR/$service" ]; then
    echo "  → Installing $service"
    cp "$TEMPLATE_DIR/$service" "$SYSTEMD_DIR/$service"
  else
    echo "  ⚠️ Warning: $service template not found in $TEMPLATE_DIR"
  fi
done

if [ -f "$TEMPLATE_DIR/console-sync.timer" ]; then
  echo "  → Installing console-sync.timer"
  cp "$TEMPLATE_DIR/console-sync.timer" "$SYSTEMD_DIR/console-sync.timer"
fi

systemctl --user daemon-reload

echo "✅ All services installed!"
echo "💡 To enable: systemctl --user enable brokergateway ordermanager notifier webhook frontend console-sync.timer"
echo "💡 To start:  systemctl --user start brokergateway ordermanager notifier webhook frontend console-sync.timer"
