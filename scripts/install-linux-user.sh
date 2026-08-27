#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${VERSION:-0.2.27}"
ARCH="${ARCH:-amd64}"
DEB="$ROOT/dist/claude-usage-widget_${VERSION}_${ARCH}.deb"

if [ ! -f "$DEB" ]; then
  DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/tmp/dotnet-home}" \
  PATH="${PATH}" \
    "$ROOT/scripts/build-deb.sh"
fi

sudo dpkg -i "$DEB"
systemctl --user daemon-reload
systemctl --user reset-failed claude-usage-widget.service || true
systemctl --user enable --now claude-usage-widget.service

if command -v kpackagetool6 >/dev/null 2>&1; then
  kpackagetool6 --type Plasma/Applet --show eu.zrnik.ai-usage-widget >/dev/null 2>&1 || true
fi

echo "Installed $DEB"
echo "Daemon: systemctl --user status claude-usage-widget.service"
