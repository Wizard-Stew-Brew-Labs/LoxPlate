#!/usr/bin/env bash
# Run the unit + contract tests inside the docker toolchain.
set -euo pipefail
cd "$(dirname "$0")/.."

if [ ! -f src/bin/Release/LoxPlate.dll ]; then
  echo "Mod DLL not built - building first"
  ./scripts/build.sh >/dev/null
fi

UID_GID="$(id -u):$(id -g)"

docker run --rm \
  --user "$UID_GID" \
  -e HOME=/tmp/home \
  -e NUGET_PACKAGES=/src/.cache/nuget \
  -v "$PWD:/src" \
  beeswax-build 'dotnet test tests/LoxPlate.Tests -c Release'
