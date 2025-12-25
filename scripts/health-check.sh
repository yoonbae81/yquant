#!/bin/bash
set -e

echo "🏥 Checking health of all yQuant services..."

SERVICES=(
  "brokergateway"
  "ordermanager"
  "notifier"
  "web"
  "webhook"
)

ALL_HEALTHY=true

for service in "${SERVICES[@]}"; do
  if systemctl --user is-active --quiet "$service.service"; then
    echo "✅ $service.service is running"
  else
    echo "❌ $service.service is NOT running"
    ALL_HEALTHY=false
  fi
done

if systemctl --user is-active --quiet "console-sync.timer"; then
  echo "✅ console-sync.timer is active"
  echo "   Next run: $(systemctl --user list-timers console-sync.timer --no-pager | grep console-sync | awk '{print $1, $2, $3}')"
else
  echo "❌ console-sync.timer is NOT active"
  ALL_HEALTHY=false
fi

echo ""
if [ "$ALL_HEALTHY" = true ]; then
  echo "✅ All services are healthy!"
  exit 0
else
  echo "❌ Some services are not running. Check logs with:"
  echo "   journalctl --user -u <service-name> -n 50"
  exit 1
fi
