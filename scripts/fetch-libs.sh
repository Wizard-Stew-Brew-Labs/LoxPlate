#!/usr/bin/env bash
# Stage compile-time libraries (BepInEx core + Harmony + Jotunn) at the exact
# versions the shipped manifest declares and the Gale "1.0" profile runs:
#   denikson-BepInExPack_Valheim-5.4.2351
#   ValheimModding-Jotunn-2.30.2
# Prefers the local Gale profile (exact installed client copies); falls back
# to Thunderstore downloads of the pinned versions. Also mirrors the sibling
# projects' staged copies, which came from the same profile.
set -euo pipefail
cd "$(dirname "$0")/.."

BEPINEX_VER="5.4.2351"
JOTUNN_VER="2.30.2"
GALE_PROFILE="${GALE_PROFILE_PATH:-$HOME/.local/share/com.kesomannen.gale/valheim/profiles/1.0}"

mkdir -p .cache/downloads libs

copy_from_profile() {
  local core="$GALE_PROFILE/BepInEx/core"
  local jotunn="$GALE_PROFILE/BepInEx/plugins/ValheimModding-Jotunn"
  [ -f "$core/BepInEx.dll" ] && [ -f "$jotunn/Jotunn.dll" ] || return 1
  # Sanity: the profile versions must match the manifest pins
  python3 - "$jotunn/manifest.json" "$JOTUNN_VER" <<'PYEOF'
import json, sys
m = json.load(open(sys.argv[1], encoding="utf-8-sig"))
if m.get("version_number") != sys.argv[2]:
    sys.exit(f"Profile Jotunn is {m.get('version_number')}, expected {sys.argv[2]}")
PYEOF
  cp "$core/BepInEx.dll" "$core/0Harmony.dll" libs/
  cp "$jotunn/Jotunn.dll" libs/
  echo -n "$JOTUNN_VER" > libs/jotunn.version
  echo "Copied BepInEx + Jotunn $JOTUNN_VER from Gale profile"
  return 0
}

copy_from_sibling() {
  local sibling="../loxcarts/libs"
  [ -f "$sibling/Jotunn.dll" ] && [ -f "$sibling/jotunn.version" ] || return 1
  if [ "$(cat "$sibling/jotunn.version")" != "$JOTUNN_VER" ]; then
    return 1
  fi
  cp "$sibling/BepInEx.dll" "$sibling/0Harmony.dll" "$sibling/Jotunn.dll" libs/
  echo -n "$JOTUNN_VER" > libs/jotunn.version
  echo "Copied BepInEx + Jotunn $JOTUNN_VER from sibling project"
  return 0
}

if copy_from_profile || copy_from_sibling; then
  ls -la libs/
  exit 0
fi

echo "Gale profile not usable at $GALE_PROFILE - downloading from Thunderstore" >&2
dl() { echo "Downloading $1"; curl -fL --retry 3 -o "$2" "$1"; }
dl "https://thunderstore.io/package/download/denikson/BepInExPack_Valheim/${BEPINEX_VER}/" ".cache/downloads/bepinex.zip"
dl "https://thunderstore.io/package/download/ValheimModding/Jotunn/${JOTUNN_VER}/" ".cache/downloads/jotunn.zip"

rm -rf .cache/downloads/x-bepinex .cache/downloads/x-jotunn
mkdir -p .cache/downloads/x-bepinex .cache/downloads/x-jotunn
unzip -q .cache/downloads/bepinex.zip -d .cache/downloads/x-bepinex
# Jotunn zips use backslash path separators; unzip mangles them, so use python
python3 - <<'PYEOF'
import zipfile, os
src, dest = '.cache/downloads/jotunn.zip', '.cache/downloads/x-jotunn'
with zipfile.ZipFile(src) as z:
    for info in z.infolist():
        name = info.filename.replace('\\', '/')
        if name.endswith('/') or not name.endswith('.dll'):
            continue
        out = os.path.join(dest, *name.split('/'))
        os.makedirs(os.path.dirname(out), exist_ok=True)
        with z.open(info) as f, open(out, 'wb') as g:
            g.write(f.read())
PYEOF

cp .cache/downloads/x-bepinex/BepInEx/core/BepInEx.dll libs/
cp .cache/downloads/x-bepinex/BepInEx/core/0Harmony.dll libs/
cp .cache/downloads/x-jotunn/Jotunn.dll libs/
echo -n "$JOTUNN_VER" > libs/jotunn.version
echo "Downloaded BepInEx $BEPINEX_VER + Jotunn $JOTUNN_VER"
ls -la libs/
