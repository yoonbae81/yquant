#!/bin/bash
# scripts/restart-dashboard.sh
set -e

echo "🔄 Restarting yQuant Dashboard service..."

systemctl --user restart web.service

if systemctl --user is-active --quiet web.service; then
  echo "✅ web.service is running"
else
  echo "❌ web.service failed to start"
  exit 1
fi

echo "✅ Dashboard service restarted!"
