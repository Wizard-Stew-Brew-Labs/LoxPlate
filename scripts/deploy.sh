#!/usr/bin/env bash
# Push the locally built LoxPlate package from dist/ into the running
# Valheim dev server container (the loxcarts one) and restart it - for fast
# dev iteration. LoxPlate coexists with LoxCarts (different plugin GUIDs).
#
# Usage: scripts/deploy.sh   (VALHEIM_CONTAINER overrides the container name)
set -euo pipefail
cd "$(dirname "$0")/.."

CONTAINER="${VALHEIM_CONTAINER:-valheim-loxcarts}"
DEST="/valheim/BepInEx/plugins/Stew_and_Brew_Labs-LoxPlate"

ZIP=$(ls dist/LoxPlate-*.zip 2>/dev/null | sort -V | tail -1)
[ -n "$ZIP" ] || { echo "ERROR: no dist/LoxPlate-*.zip - run scripts/package.sh first" >&2; exit 1; }
docker inspect "$CONTAINER" >/dev/null 2>&1 || { echo "ERROR: container '$CONTAINER' not found" >&2; exit 1; }
WAS_RUNNING=$(docker inspect -f '{{.State.Running}}' "$CONTAINER")

TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT
unzip -q "$ZIP" 'BepInEx/plugins/LoxPlate.dll' 'manifest.json' 'icon.png' -d "$TMP"
[ -f "$TMP/BepInEx/plugins/LoxPlate.dll" ] || { echo "ERROR: LoxPlate.dll missing inside $ZIP" >&2; exit 1; }

docker exec "$CONTAINER" mkdir -p "$DEST"
docker cp "$TMP/BepInEx/plugins/LoxPlate.dll" "$CONTAINER:$DEST/LoxPlate.dll"
docker cp "$TMP/manifest.json" "$CONTAINER:$DEST/manifest.json"
docker cp "$TMP/icon.png" "$CONTAINER:$DEST/icon.png"

if [ "$WAS_RUNNING" = "true" ]; then
  docker restart "$CONTAINER" >/dev/null
  echo "deployed $(basename "$ZIP") to $CONTAINER and restarted it"
else
  echo "deployed $(basename "$ZIP") to $CONTAINER (was stopped, left stopped)"
fi
