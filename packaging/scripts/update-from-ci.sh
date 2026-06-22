#!/usr/bin/env bash
set -euo pipefail

REPO="${REPO:-Zrnik/claude-usage-windows-taskbar-widget}"
WORKFLOW="${WORKFLOW:-release.yml}"
ARTIFACT="${ARTIFACT:-linux-packages}"
BRANCH="${BRANCH:-}"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/ai-usage-widget-update.XXXXXX")"

cleanup() {
  rm -rf "$TMP_DIR"
}
trap cleanup EXIT

api_get() {
  curl -fsSL -H "Accept: application/vnd.github+json" "$1"
}

echo "Finding latest successful CI package build..."
release_json="$TMP_DIR/release.json"
download_url=""
if api_get "https://api.github.com/repos/$REPO/releases/latest" > "$release_json" 2>/dev/null; then
  download_url="$(python3 - "$release_json" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as f:
    release = json.load(f)
for asset in release.get("assets", []):
    name = asset.get("name", "")
    if name.endswith("_amd64.deb") or name.endswith(".deb"):
        print(asset["browser_download_url"])
        break
PY
)"
fi

if [ -z "$download_url" ]; then
  echo "No .deb release asset found; trying GitHub Actions artifacts..."
fi

query="https://api.github.com/repos/$REPO/actions/workflows/$WORKFLOW/runs?status=success&per_page=20"
if [ -n "$BRANCH" ]; then
  query="$query&branch=$BRANCH"
fi

if [ -z "$download_url" ]; then
  runs_json="$TMP_DIR/runs.json"
  api_get "$query" > "$runs_json"

  mapfile -t artifact_urls < <(python3 - "$runs_json" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as f:
    runs = json.load(f).get("workflow_runs", [])
for run in runs:
    if run.get("conclusion") == "success":
        print(run["artifacts_url"])
PY
)

  for i in "${!artifact_urls[@]}"; do
    artifacts_json="$TMP_DIR/artifacts-$i.json"
    api_get "${artifact_urls[$i]}" > "$artifacts_json"
    candidate="$(python3 - "$artifacts_json" "$ARTIFACT" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as f:
    artifacts = json.load(f).get("artifacts", [])
name = sys.argv[2]
for artifact in artifacts:
    if artifact.get("name") == name and not artifact.get("expired", False):
        print(artifact["archive_download_url"])
        break
PY
)"
    if [ -n "$candidate" ]; then
      download_url="$candidate"
      break
    fi
  done

  if [ -z "$download_url" ]; then
    echo "Artifact $ARTIFACT not found in recent successful workflow runs" >&2
    exit 1
  fi
fi

case "$download_url" in
  *.deb)
    deb="$TMP_DIR/package.deb"
    echo "Downloading $(basename "$download_url")..."
    curl -fsSL -L -o "$deb" "$download_url"
    ;;
  *)
    echo "Downloading $ARTIFACT..."
    curl -fsSL -L -H "Accept: application/vnd.github+json" -o "$TMP_DIR/artifact.zip" "$download_url"
    unzip -q "$TMP_DIR/artifact.zip" -d "$TMP_DIR/package"
    deb="$(find "$TMP_DIR/package" -maxdepth 1 -type f -name '*.deb' | sort -V | tail -n 1)"
    if [ -z "$deb" ]; then
      echo "No .deb package found in CI artifact" >&2
      exit 1
    fi
    ;;
esac

if ! dpkg-deb --field "$deb" Package >/dev/null 2>&1; then
  echo "Downloaded file is not a Debian package" >&2
  exit 1
fi

echo "Installing $(basename "$deb")..."
if command -v pkexec >/dev/null 2>&1; then
  pkexec dpkg -i "$deb"
else
  sudo dpkg -i "$deb"
fi

systemctl --user daemon-reload || true
systemctl --user restart claude-usage-widget.service || true

if command -v kbuildsycoca6 >/dev/null 2>&1; then
  kbuildsycoca6 --noincremental || true
fi

if systemctl --user list-unit-files plasma-plasmashell.service >/dev/null 2>&1; then
  systemctl --user restart plasma-plasmashell.service || true
fi

echo "AI Usage Widget updated from CI."
