#!/bin/bash
# scripts/setup-web.sh
set -e

echo "⚙️ Setting up yQuant Web service (systemd)..."

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SYSTEMD_DIR="$HOME/.config/systemd/user"
TEMPLATE_DIR="$PROJECT_ROOT/scripts/systemd"

mkdir -p "$SYSTEMD_DIR"

echo "  → Installing web.service"
cp "$TEMPLATE_DIR/web.service" "$SYSTEMD_DIR/web.service"

systemctl --user daemon-reload

echo "✅ Web service installed!"
echo "💡 To enable: systemctl --user enable web"
echo "💡 To start:  systemctl --user start web"
