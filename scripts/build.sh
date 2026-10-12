#!/usr/bin/env bash
# Compile the mod inside the docker toolchain.
set -euo pipefail
cd "$(dirname "$0")/.."

UID_GID="$(id -u):$(id -g)"

docker run --rm \
  --user "$UID_GID" \
  -e HOME=/tmp/home \
  -e NUGET_PACKAGES=/src/.cache/nuget \
  -v "$PWD:/src" \
  beeswax-build 'dotnet build src/LoxPlate.csproj -c Release --no-incremental'
