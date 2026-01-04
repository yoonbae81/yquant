#!/bin/bash
# scripts/restart.sh
set -e

echo "🔄 Restarting all yQuant services..."

SERVICES=(
  "brokergateway"
  "ordermanager"
  "notifier"
  "webhook"
  "dashboard"
)

for service in "${SERVICES[@]}"; do
  echo "🔄 Restarting $service.service..."
  systemctl --user restart "$service.service"
  
  if systemctl --user is-active --quiet "$service.service"; then
    echo "✅ $service.service is running"
  else
    echo "❌ $service.service failed to start"
    exit 1
  fi
done

echo "✅ All services restarted!"
