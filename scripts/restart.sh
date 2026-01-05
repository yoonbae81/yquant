#!/bin/bash
# scripts/restart.sh
set -e

TYPE=$1
echo "🔄 Restarting yQuant services (Target: ${TYPE:-all})..."

SERVICES=()
TIMERS=()

if [ "$TYPE" == "port" ]; then
    TIMERS=("console-sync.timer")
elif [ "$TYPE" == "node" ]; then
    SERVICES=("brokergateway" "ordermanager" "notifier" "webhook" "dashboard")
else
    SERVICES=("brokergateway" "ordermanager" "notifier" "webhook" "dashboard")
    TIMERS=("console-sync.timer")
fi

# Restart Services
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

# Restart Timers
for timer in "${TIMERS[@]}"; do
  echo "🔄 Restarting $timer..."
  systemctl --user restart "$timer"
  
  if systemctl --user is-active --quiet "$timer"; then
    echo "✅ $timer is active"
  else
    echo "❌ $timer failed to start"
    exit 1
  fi
done

echo "✅ Restart process completed!"
