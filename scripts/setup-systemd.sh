#!/bin/bash
set -e

echo "⚙️  Setting up systemd user services for yQuant..."

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SYSTEMD_DIR="$HOME/.config/systemd/user"
TEMPLATE_DIR="$PROJECT_ROOT/scripts/systemd"

echo "📁 Creating systemd user directory..."
mkdir -p "$SYSTEMD_DIR"

# Environment variables are now handled via appsecrets.json or appsettings.json in each application directory.

echo "📋 Installing service files..."

SERVICES=(
  "brokergateway.service"
  "ordermanager.service"
  "notifier.service"
  "web.service"
  "webhook.service"
  "console-sync.service"
)

for service in "${SERVICES[@]}"; do
  echo "  → Installing $service"
  cp "$TEMPLATE_DIR/$service" "$SYSTEMD_DIR/$service"
done

echo "  → Installing console-sync.timer"
cp "$TEMPLATE_DIR/console-sync.timer" "$SYSTEMD_DIR/console-sync.timer"

echo ""
echo "🔄 Reloading systemd daemon..."
systemctl --user daemon-reload

echo ""
echo "✅ Systemd services installed successfully!"
echo ""
echo "📝 Next steps:"
echo ""
echo "   1. Enable and start services:"
echo "      systemctl --user enable brokergateway ordermanager notifier web webhook console-sync.timer"
echo "      systemctl --user start brokergateway ordermanager notifier web webhook console-sync.timer"
echo ""
echo "   2. Enable linger (services run after logout):"
echo "      sudo loginctl enable-linger \$USER"
echo ""
echo "   3. Check service status:"
echo "      systemctl --user status"
