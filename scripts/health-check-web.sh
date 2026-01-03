#!/bin/bash
# scripts/health-check-web.sh
set -e

echo "🏥 Checking health of yQuant Web service..."

if systemctl --user is-active --quiet "web.service"; then
  echo "✅ web.service is running"
  echo "✅ Web service is healthy!"
  exit 0
else
  echo "❌ web.service is NOT running"
  echo "❌ Web service failed. Check logs with:"
  echo "   journalctl --user -u web -n 50"
  exit 1
fi
