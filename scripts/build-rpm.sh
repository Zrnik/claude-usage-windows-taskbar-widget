#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${VERSION:-0.2.25}"
RELEASE="${RELEASE:-1}"
ARCH="${ARCH:-x86_64}"
RID="${RID:-linux-x64}"
PKG="claude-usage-widget"
PLASMOID_ID="eu.zrnik.ai-usage-widget"
WORK="$ROOT/dist/rpm"
PAYLOAD="$WORK/payload"
PUBLISH="$ROOT/dist/publish/linux-daemon-rpm"
TOPDIR="$WORK/rpmbuild"
SPEC="$TOPDIR/SPECS/${PKG}.spec"

rm -rf "$WORK" "$PUBLISH"
mkdir -p "$PAYLOAD/usr/lib/claude-usage-widget" \
  "$PAYLOAD/usr/lib/systemd/user" \
  "$PAYLOAD/usr/bin" \
  "$PAYLOAD/usr/share/plasma/plasmoids/$PLASMOID_ID" \
  "$TOPDIR/BUILD" "$TOPDIR/BUILDROOT" "$TOPDIR/RPMS" "$TOPDIR/SOURCES" "$TOPDIR/SPECS" "$TOPDIR/SRPMS"

dotnet publish "$ROOT/src/ClaudeUsageWidget.LinuxDaemon/ClaudeUsageWidget.LinuxDaemon.csproj" \
  -c Release -r "$RID" --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -p:Version="$VERSION" \
  -p:AssemblyVersion="${VERSION}.0" \
  -p:FileVersion="${VERSION}.0" \
  -o "$PUBLISH"

cp "$PUBLISH/ClaudeUsageWidget.LinuxDaemon" "$PAYLOAD/usr/lib/claude-usage-widget/"
cp "$ROOT/packaging/scripts/update-from-ci.sh" "$PAYLOAD/usr/bin/ai-usage-widget-update"
cp "$ROOT/packaging/systemd/claude-usage-widget.service" "$PAYLOAD/usr/lib/systemd/user/"
cp -a "$ROOT/plasmoid/." "$PAYLOAD/usr/share/plasma/plasmoids/$PLASMOID_ID/"
chmod 0755 "$PAYLOAD/usr/lib/claude-usage-widget/ClaudeUsageWidget.LinuxDaemon"
chmod 0755 "$PAYLOAD/usr/bin/ai-usage-widget-update"
sed -i "s/\"Version\": \".*\"/\"Version\": \"${VERSION}\"/" "$PAYLOAD/usr/share/plasma/plasmoids/$PLASMOID_ID/metadata.json"

cat > "$SPEC" <<'SPEC'
Name: claude-usage-widget
Version: %{version}
Release: %{release}%{?dist}
Summary: Claude, Codex, Toggl and JIRA usage widget for KDE Plasma
License: MIT
URL: https://github.com/zrnik/claude-usage-windows-taskbar-widget
BuildArch: %{pkg_arch}

Requires: systemd
Requires: plasma-workspace

%description
A native KDE Plasma widget backed by a local daemon. It shows Claude and
Codex usage plus optional Toggl Track and JIRA metrics.

%prep

%build

%install
mkdir -p %{buildroot}
cp -a %{payload_dir}/. %{buildroot}/

%files
/usr/lib/claude-usage-widget/ClaudeUsageWidget.LinuxDaemon
/usr/lib/systemd/user/claude-usage-widget.service
/usr/bin/ai-usage-widget-update
/usr/share/plasma/plasmoids/eu.zrnik.ai-usage-widget

%post
systemctl --user daemon-reload >/dev/null 2>&1 || true

%preun
if [ "$1" = "0" ]; then
  systemctl --user disable --now claude-usage-widget.service >/dev/null 2>&1 || true
fi
SPEC

rpmbuild -bb "$SPEC" \
  --define "_topdir $TOPDIR" \
  --define "version $VERSION" \
  --define "release $RELEASE" \
  --define "pkg_arch $ARCH" \
  --define "payload_dir $PAYLOAD"

mkdir -p "$ROOT/dist"
RPM_PATH="$(find "$TOPDIR/RPMS" -type f -name "${PKG}-${VERSION}-${RELEASE}*.rpm" | head -n 1)"
cp "$RPM_PATH" "$ROOT/dist/"
echo "$ROOT/dist/$(basename "$RPM_PATH")"
