#!/bin/bash
# scripts/restart-backend.sh
set -e

echo "🔄 Restarting yQuant Backend services..."

SERVICES=(
  "brokergateway"
  "ordermanager"
  "notifier"
  "webhook"
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

echo "✅ Backend services restarted!"
