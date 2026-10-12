#!/usr/bin/env bash
# Verify the packaged zip is structurally valid for Thunderstore.
# Run scripts/package.sh first. Requires: unzip, python3.
set -euo pipefail
cd "$(dirname "$0")/.."

VERSION="$(grep -oPm1 '(?<=<Version>)[^<]+' src/LoxPlate.csproj)"
ZIP="dist/LoxPlate-$VERSION.zip"

if [ ! -f "$ZIP" ]; then
  echo "FAIL: $ZIP not found - run scripts/package.sh first" >&2
  exit 1
fi

echo "Checking $ZIP"
unzip -l "$ZIP" | grep -q ' manifest.json$' || { echo "FAIL: manifest.json not at zip root"; exit 1; }
unzip -l "$ZIP" | grep -q 'BepInEx/plugins/LoxPlate.dll$' || { echo "FAIL: plugin DLL missing"; exit 1; }
unzip -l "$ZIP" | grep -q 'BepInEx/plugins/icon.png$' || { echo "FAIL: plugins/icon.png missing (mod-manager thumbnails)"; exit 1; }
unzip -l "$ZIP" | grep -q ' icon.png$' || { echo "FAIL: icon.png missing at root"; exit 1; }
unzip -l "$ZIP" | grep -q ' README.md$' || { echo "FAIL: README.md missing"; exit 1; }
unzip -l "$ZIP" | grep -q ' CHANGELOG.md$' || { echo "FAIL: CHANGELOG.md missing"; exit 1; }

python3 - "$ZIP" "$VERSION" <<'EOF'
import json, struct, sys, zipfile

z = zipfile.ZipFile(sys.argv[1])
csproj_version = sys.argv[2]

m = json.loads(z.read("manifest.json"))
assert set(m) >= {"name", "version_number", "website_url", "description", "dependencies"}, "manifest missing required keys"
assert m["name"].isalnum(), "manifest name must be alphanumeric"
assert m["version_number"] == csproj_version, (
    f"version drift: manifest {m['version_number']} != csproj {csproj_version}")
assert len(m["description"]) <= 250, "description exceeds 250 chars"
for d in m["dependencies"]:
    parts = d.split("-")
    assert len(parts) == 3 and all(parts), f"bad dependency string: {d}"
    assert "@@" not in d, f"unsubstituted template variable in {d}"

data = z.read("icon.png")
assert data[:8] == b"\x89PNG\r\n\x1a\n", "icon.png is not a PNG"
w, h = struct.unpack(">II", data[16:24])
assert (w, h) == (256, 256), f"icon must be 256x256, got {w}x{h}"

dll = z.read("BepInEx/plugins/LoxPlate.dll")
assert dll[:2] == b"MZ" and len(dll) > 1024, "plugin DLL looks invalid"
EOF

echo "OK: manifest v$VERSION, dependencies, icon (256x256), DLL and changelog all valid"
