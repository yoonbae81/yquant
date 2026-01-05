#!/bin/bash
# scripts/switch-active.sh
# yq-port 서버에서 실행하여 Blue와 Green의 역할을 교체합니다.

set -e

if [ "$#" -ne 1 ]; then
    echo "Usage: $0 [blue|green]"
    exit 1
fi

TARGET=$1
# 실제 운영 환경의 HAProxy 설정 경로
HAPROXY_CONF="/etc/haproxy/haproxy.cfg"

echo "🔄 Switching Active Node to: $TARGET"

# 1. 모든 서버 라인에서 backup 키워드 제거 (초기화)
# 'server blue' 또는 'server green'이 포함된 라인에서 ' backup' 제거
sudo sed -i '/server \(blue\|green\)/s/ backup//' $HAPROXY_CONF

# 2. 선택되지 않은(Target이 아닌) 서버에 backup 키워드 추가
if [ "$TARGET" == "blue" ]; then
    sudo sed -i '/server green/s/check/check backup/' $HAPROXY_CONF
elif [ "$TARGET" == "green" ]; then
    sudo sed -i '/server blue/s/check/check backup/' $HAPROXY_CONF
else
    echo "❌ Invalid target: $TARGET. Please choice 'blue' or 'green'."
    exit 1
fi

# 3. 설정 문법 검사 후 반영
if sudo haproxy -c -f $HAPROXY_CONF > /dev/null 2>&1; then
    sudo systemctl reload haproxy
    echo "✅ Switch completed. $TARGET node is now Active."
else
    echo "❌ HAProxy configuration validation failed!"
    exit 1
fi
