#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${VERSION:-0.2.27}"
ARCH="${ARCH:-amd64}"
RID="${RID:-linux-x64}"
PKG="claude-usage-widget"
PLASMOID_ID="eu.zrnik.ai-usage-widget"
STAGE="$ROOT/dist/deb/${PKG}_${VERSION}_${ARCH}"
PUBLISH="$ROOT/dist/publish/linux-daemon"

rm -rf "$STAGE" "$PUBLISH"
mkdir -p "$STAGE/DEBIAN" \
  "$STAGE/usr/lib/claude-usage-widget" \
  "$STAGE/usr/lib/systemd/user" \
  "$STAGE/usr/bin" \
  "$STAGE/usr/share/plasma/plasmoids/$PLASMOID_ID"

dotnet publish "$ROOT/src/ClaudeUsageWidget.LinuxDaemon/ClaudeUsageWidget.LinuxDaemon.csproj" \
  -c Release -r "$RID" --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -p:Version="$VERSION" \
  -p:AssemblyVersion="${VERSION}.0" \
  -p:FileVersion="${VERSION}.0" \
  -o "$PUBLISH"

cp "$PUBLISH/ClaudeUsageWidget.LinuxDaemon" "$STAGE/usr/lib/claude-usage-widget/"
cp "$ROOT/packaging/scripts/update-from-ci.sh" "$STAGE/usr/bin/ai-usage-widget-update"
cp "$ROOT/packaging/systemd/claude-usage-widget.service" "$STAGE/usr/lib/systemd/user/"
cp -a "$ROOT/plasmoid/." "$STAGE/usr/share/plasma/plasmoids/$PLASMOID_ID/"
cp "$ROOT/packaging/debian/control" "$STAGE/DEBIAN/control"
cp "$ROOT/packaging/debian/postinst" "$STAGE/DEBIAN/postinst"
cp "$ROOT/packaging/debian/prerm" "$STAGE/DEBIAN/prerm"

chmod 0755 "$STAGE/DEBIAN/postinst" "$STAGE/DEBIAN/prerm"
chmod 0755 "$STAGE/usr/lib/claude-usage-widget/ClaudeUsageWidget.LinuxDaemon"
chmod 0755 "$STAGE/usr/bin/ai-usage-widget-update"

sed -i "s/^Version:.*/Version: ${VERSION}/" "$STAGE/DEBIAN/control"
sed -i "s/^Architecture:.*/Architecture: ${ARCH}/" "$STAGE/DEBIAN/control"
sed -i "s/\"Version\": \".*\"/\"Version\": \"${VERSION}\"/" "$STAGE/usr/share/plasma/plasmoids/$PLASMOID_ID/metadata.json"

dpkg-deb --root-owner-group --build "$STAGE" "$ROOT/dist/${PKG}_${VERSION}_${ARCH}.deb"
echo "$ROOT/dist/${PKG}_${VERSION}_${ARCH}.deb"
