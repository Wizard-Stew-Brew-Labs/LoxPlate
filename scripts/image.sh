#!/usr/bin/env bash
# Build the toolchain image (dotnet 8 + steamcmd + ilspycmd) if it does not
# already exist. Reuses the shared beeswax-build image, which every mod
# project on this machine builds against; the build context lives in the
# beeswax-coating repo.
set -euo pipefail
cd "$(dirname "$0")/.."

if docker image inspect beeswax-build >/dev/null 2>&1; then
  echo "beeswax-build image already present"
  exit 0
fi
CONTEXT="../loxcarts/docker"
[ -d "$CONTEXT" ] || CONTEXT="../beeswax-coating/docker"
docker build -t beeswax-build "$CONTEXT"
