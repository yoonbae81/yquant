#!/bin/bash
# scripts/node/health-check.sh
set -e

SERVICE=$1
echo "🏥 Checking health of yQuant NODE services (Target: ${SERVICE:-all})..."

check_service() {
    local name=$1
    if systemctl --user is-active --quiet "$name.service"; then
        echo "✅ $name.service is running"
    else
        echo "❌ $name.service is NOT running"
        return 1
    fi
}

ALL_HEALTHY=true

if [ -n "$SERVICE" ]; then
    check_service "$SERVICE" || ALL_HEALTHY=false
else
    SERVICES=("brokergateway" "ordermanager" "notifier" "webhook" "dashboard")
    for s in "${SERVICES[@]}"; do
        check_service "$s" || ALL_HEALTHY=false
    done
fi

if [ "$ALL_HEALTHY" = true ]; then
    echo "✅ Targeted node services are healthy!"
else
    echo "❌ Some node services are unhealthy!"
    exit 1
fi
