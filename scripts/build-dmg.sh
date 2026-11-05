#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
APP="${1:-build/DiskSpace.app}"
DMG="build/DiskSpace-macOS.dmg"
test -d "$APP"
codesign --verify --strict "$APP"
WORK_DIR="$(mktemp -d)"
MOUNTED=0
cleanup() {
  if [[ "$MOUNTED" = 1 ]]; then hdiutil detach "$WORK_DIR/mount"; fi
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT
mkdir -p "$WORK_DIR/stage" "$WORK_DIR/mount" build
ditto "$APP" "$WORK_DIR/stage/DiskSpace.app"
ln -s /Applications "$WORK_DIR/stage/Applications"
hdiutil create -ov -volname DiskSpace -srcfolder "$WORK_DIR/stage" -format UDZO "$DMG"
hdiutil verify "$DMG"
hdiutil attach "$DMG" -mountpoint "$WORK_DIR/mount" -readonly -nobrowse
MOUNTED=1
test -x "$WORK_DIR/mount/DiskSpace.app/Contents/MacOS/DiskSpace"
codesign --verify --strict "$WORK_DIR/mount/DiskSpace.app"
echo "DMGの作成・マウント・アプリ署名検証に成功: $DMG"
