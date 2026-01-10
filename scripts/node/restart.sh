#!/bin/bash
# scripts/node/restart.sh
set -e

SERVICE=$1
echo "🔄 Restarting yQuant NODE services (Target: ${SERVICE:-all})..."

restart_service() {
  local name=$1
  echo "🔄 Restarting $name.service..."
  systemctl --user restart "$name.service"
  
  if systemctl --user is-active --quiet "$name.service"; then
    echo "✅ $name.service is running"
  else
    echo "❌ $name.service failed to start"
    exit 1
  fi
}

if [ -n "$SERVICE" ]; then
    restart_service "$SERVICE"
else
    SERVICES=("brokergateway" "ordermanager" "notifier" "webhook" "dashboard")
    for s in "${SERVICES[@]}"; do
        restart_service "$s"
    done
fi

echo "✅ Node restart process completed!"
