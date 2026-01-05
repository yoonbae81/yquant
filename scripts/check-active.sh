#!/bin/bash
# scripts/check-active.sh
# yq-port 서버에서 실행하여 현재 어떤 노드가 Active 인지 확인합니다.

HAPROXY_CONF="/etc/haproxy/haproxy.cfg"

if [ ! -f "$HAPROXY_CONF" ]; then
    echo "❌ HAProxy configuration not found at $HAPROXY_CONF"
    exit 1
fi

# webhook_nodes 백엔드에서 backup이 없는 서버를 찾음
ACTIVE_NODE=$(grep "server" "$HAPROXY_CONF" | grep "webhook_nodes" -A 5 | grep -v "backup" | grep "server" | awk '{print $2}')

if [ -n "$ACTIVE_NODE" ]; then
    echo "🔵 Current ACTIVE Node: $ACTIVE_NODE"
else
    echo "⚠️  Could not determine active node (Configuration might be in an inconsistent state)"
fi
