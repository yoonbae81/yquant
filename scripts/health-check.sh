#!/bin/bash
# scripts/health-check.sh
set -e

echo "🏥 Checking health of all yQuant services..."

# 노드 역할 확인
NODE_NAME=$(hostname)
ROLE="Unknown"
if [ -f "/etc/haproxy/haproxy.cfg" ]; then
    if ! grep -q "server $NODE_NAME.*backup" /etc/haproxy/haproxy.cfg; then
        ROLE="ACTIVE (Webhook Traffic)"
    else
        ROLE="Standby"
    fi
fi
echo "📍 Node: $NODE_NAME | Role: $ROLE"
echo "------------------------------------------"

SERVICES=(
  "brokergateway"
  "ordermanager"
  "notifier"
  "webhook"
  "dashboard"
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
else
  echo "❌ console-sync.timer is NOT active"
  ALL_HEALTHY=false
fi

# Valkey & Sentinel Check
echo "🔍 Checking Valkey & Sentinel status..."
if command -v valkey-cli &> /dev/null; then
    if valkey-cli ping | grep -q PONG; then
        echo "✅ Valkey is responsive"
    else
        echo "❌ Valkey is NOT responsive"
        ALL_HEALTHY=false
    fi
    
    if valkey-cli -p 26379 sentinel masters 2>/dev/null | grep -q mymaster; then
        echo "✅ Sentinel is monitoring mymaster"
    else
        echo "⚠️ Sentinel might not be running or monitoring"
    fi
fi

echo ""
if [ "$ALL_HEALTHY" = true ]; then
  echo "✅ All services are healthy!"
  exit 0
else
  echo "❌ Some services are not running. Check logs with:"
  echo "   journalctl --user -t <service-name> -n 50"
  exit 1
fi
