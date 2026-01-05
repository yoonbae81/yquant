#!/bin/bash
# scripts/node/setup.sh
set -e

SERVICE=$1
echo "⚙️ Setting up yQuant NODE services (Target: ${SERVICE:-all})..."

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SYSTEMD_DIR="$HOME/.config/systemd/user"
TEMPLATE_DIR="$PROJECT_ROOT/scripts/systemd"

mkdir -p "$SYSTEMD_DIR"

install_service() {
    local name=$1
    if [ -f "$TEMPLATE_DIR/$name.service" ]; then
        echo "  → Installing $name.service"
        cp "$TEMPLATE_DIR/$name.service" "$SYSTEMD_DIR/$name.service"
    else
        echo "  ⚠️ Warning: $name.service template not found in $TEMPLATE_DIR"
    fi
}

if [ -n "$SERVICE" ]; then
    install_service "$SERVICE"
else
    SERVICES=("brokergateway" "ordermanager" "notifier" "webhook" "dashboard")
    for s in "${SERVICES[@]}"; do
        install_service "$s"
    done
fi

systemctl --user daemon-reload

echo "✅ Node setup process completed!"
