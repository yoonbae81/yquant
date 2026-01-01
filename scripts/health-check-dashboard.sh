#!/bin/bash
# scripts/health-check-dashboard.sh
set -e

echo "🏥 Checking health of yQuant Dashboard service..."

if systemctl --user is-active --quiet "web.service"; then
  echo "✅ web.service is running"
  echo "✅ Dashboard service is healthy!"
  exit 0
else
  echo "❌ web.service is NOT running"
  echo "❌ Dashboard service failed. Check logs with:"
  echo "   journalctl --user -u web -n 50"
  exit 1
fi
