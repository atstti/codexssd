#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ "$(uname -s)" != Darwin ]]; then
  echo "DiskSpace.app のビルドには macOS と Xcode Command Line Tools が必要です。" >&2
  exit 1
fi
swift build -c release
BIN_DIR="$(swift build -c release --show-bin-path)"
APP="build/DiskSpace.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN_DIR/DiskSpace" "$APP/Contents/MacOS/DiskSpace"
cp scripts/Info.plist "$APP/Contents/Info.plist"
codesign --force --sign - "$APP"
codesign --verify --strict "$APP"
ditto -c -k --sequesterRsrc --keepParent "$APP" build/DiskSpace-macOS.zip
echo "作成: $APP / build/DiskSpace-macOS.zip"
