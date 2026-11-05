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
swift scripts/generate-icon.swift build/DiskSpace.iconset
iconutil -c icns build/DiskSpace.iconset -o "$APP/Contents/Resources/DiskSpace.icns"
codesign --force --sign - "$APP"
codesign --verify --strict "$APP"
# Use a conventional ZIP without Apple resource-fork extensions.
# Archive from the parent directory so extraction produces DiskSpace.app.
python3 - <<'PY'
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile

root = Path('build')
with ZipFile(root / 'DiskSpace-macOS.zip', 'w', ZIP_DEFLATED) as archive:
    for path in sorted((root / 'DiskSpace.app').rglob('*')):
        archive.write(path, path.relative_to(root))
with ZipFile(root / 'DiskSpace-macOS.zip') as archive:
    assert archive.testzip() is None
    executable = archive.getinfo('DiskSpace.app/Contents/MacOS/DiskSpace')
    assert (executable.external_attr >> 16) & 0o111
PY
echo "作成: $APP / build/DiskSpace-macOS.zip"
