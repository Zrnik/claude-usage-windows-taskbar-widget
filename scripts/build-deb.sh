#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${VERSION:-0.2.12}"
ARCH="${ARCH:-amd64}"
RID="${RID:-linux-x64}"
PKG="claude-usage-widget"
STAGE="$ROOT/dist/deb/${PKG}_${VERSION}_${ARCH}"
PUBLISH="$ROOT/dist/publish/linux-daemon"

rm -rf "$STAGE" "$PUBLISH"
mkdir -p "$STAGE/DEBIAN" \
  "$STAGE/usr/lib/claude-usage-widget" \
  "$STAGE/usr/lib/systemd/user" \
  "$STAGE/usr/share/plasma/plasmoids/org.zrnik.claude-usage-widget"

dotnet publish "$ROOT/src/ClaudeUsageWidget.LinuxDaemon/ClaudeUsageWidget.LinuxDaemon.csproj" \
  -c Release -r "$RID" --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -o "$PUBLISH"

cp "$PUBLISH/ClaudeUsageWidget.LinuxDaemon" "$STAGE/usr/lib/claude-usage-widget/"
cp "$ROOT/packaging/systemd/claude-usage-widget.service" "$STAGE/usr/lib/systemd/user/"
cp -a "$ROOT/plasmoid/." "$STAGE/usr/share/plasma/plasmoids/org.zrnik.claude-usage-widget/"
cp "$ROOT/packaging/debian/control" "$STAGE/DEBIAN/control"
cp "$ROOT/packaging/debian/postinst" "$STAGE/DEBIAN/postinst"
cp "$ROOT/packaging/debian/prerm" "$STAGE/DEBIAN/prerm"

chmod 0755 "$STAGE/DEBIAN/postinst" "$STAGE/DEBIAN/prerm"
chmod 0755 "$STAGE/usr/lib/claude-usage-widget/ClaudeUsageWidget.LinuxDaemon"

sed -i "s/^Version:.*/Version: ${VERSION}/" "$STAGE/DEBIAN/control"
sed -i "s/^Architecture:.*/Architecture: ${ARCH}/" "$STAGE/DEBIAN/control"

dpkg-deb --root-owner-group --build "$STAGE" "$ROOT/dist/${PKG}_${VERSION}_${ARCH}.deb"
echo "$ROOT/dist/${PKG}_${VERSION}_${ARCH}.deb"
