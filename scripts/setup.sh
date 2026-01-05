#!/bin/bash
# scripts/setup.sh
set -e

TYPE=$1
echo "⚙️ Setting up yQuant services (Target: ${TYPE:-all})..."

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SYSTEMD_DIR="$HOME/.config/systemd/user"
TEMPLATE_DIR="$PROJECT_ROOT/scripts/systemd"

mkdir -p "$SYSTEMD_DIR"

SERVICES=()
INSTALL_TIMER=false

if [ "$TYPE" == "port" ]; then
    SERVICES=("console-sync.service")
    INSTALL_TIMER=true
elif [ "$TYPE" == "node" ]; then
    SERVICES=(
        "brokergateway.service"
        "ordermanager.service"
        "notifier.service"
        "webhook.service"
        "dashboard.service"
    )
else
    SERVICES=(
        "brokergateway.service"
        "ordermanager.service"
        "notifier.service"
        "webhook.service"
        "dashboard.service"
        "console-sync.service"
    )
    INSTALL_TIMER=true
fi

# Install Services
for service in "${SERVICES[@]}"; do
  if [ -f "$TEMPLATE_DIR/$service" ]; then
    echo "  → Installing $service"
    cp "$TEMPLATE_DIR/$service" "$SYSTEMD_DIR/$service"
  else
    echo "  ⚠️ Warning: $service template not found in $TEMPLATE_DIR"
  fi
done

# Install Timer if needed
if [ "$INSTALL_TIMER" = true ]; then
    if [ -f "$TEMPLATE_DIR/console-sync.timer" ]; then
        echo "  → Installing console-sync.timer"
        cp "$TEMPLATE_DIR/console-sync.timer" "$SYSTEMD_DIR/console-sync.timer"
    fi
fi

systemctl --user daemon-reload

echo "✅ Setup process completed!"

if [ "$TYPE" == "port" ]; then
    echo "💡 To enable: systemctl --user enable console-sync.timer"
    echo "💡 To start:  systemctl --user start console-sync.timer"
elif [ "$TYPE" == "node" ]; then
    echo "💡 To enable: systemctl --user enable brokergateway ordermanager notifier webhook dashboard"
    echo "💡 To start:  systemctl --user start brokergateway ordermanager notifier webhook dashboard"
else
    echo "💡 To enable: systemctl --user enable brokergateway ordermanager notifier webhook dashboard console-sync.timer"
    echo "💡 To start:  systemctl --user start brokergateway ordermanager notifier webhook dashboard console-sync.timer"
fi
