#!/usr/bin/env bash
# Package the Thunderstore-ready zip into dist/.
set -euo pipefail
cd "$(dirname "$0")/.."

VERSION="$(grep -oPm1 '(?<=<Version>)[^<]+' src/LoxPlate.csproj)"
JOTUNN_VER="$(cat libs/jotunn.version)"
OUT="dist"

DLL="src/bin/Release/LoxPlate.dll"
if [ ! -f "$DLL" ]; then
  echo "ERROR: $DLL not found - run scripts/build.sh first" >&2
  exit 1
fi

if [ ! -f packaging/icon.png ]; then
  echo "ERROR: packaging/icon.png missing" >&2
  exit 1
fi

rm -rf "$OUT" && mkdir -p "$OUT/stage/BepInEx/plugins"
cp "$DLL" "$OUT/stage/BepInEx/plugins/LoxPlate.dll"
cp README.md "$OUT/stage/"
cp CHANGELOG.md "$OUT/stage/"
cp packaging/icon.png "$OUT/stage/"
# Second copy inside BepInEx/plugins/: mod managers (Gale) only look for
# icon.png in the extracted install dir, not zip metadata - BepInEx ignores
# non-DLL files there, so this is harmless and makes the thumbnail show.
cp packaging/icon.png "$OUT/stage/BepInEx/plugins/"
sed "s/@JOTUNN@/$JOTUNN_VER/" packaging/manifest.json > "$OUT/stage/manifest.json"

# Thunderstore requires manifest.json at the zip root
(
  cd "$OUT/stage"
  zip -qr "../LoxPlate-$VERSION.zip" .
)
rm -rf "$OUT/stage"

echo "Packaged dist/LoxPlate-$VERSION.zip"
