#!/usr/bin/env bash
# Materialize the game reference assemblies for compiling against the current
# (1.0+) dedicated server build.
#
# Reuses an existing steamcmd cache from the sibling projects when present
# (saves the ~2 GB download), otherwise downloads via steamcmd into the local
# cache. Either way the cache is steamcmd-validated so refs always match the
# current public server build. Only the managed assemblies the mod compiles
# against are copied into refs/ (gitignored).
set -euo pipefail
cd "$(dirname "$0")/.."

UID_GID="$(id -u):$(id -g)"

mkdir -p .cache
if [ ! -f .cache/valheim-server/valheim_server.x86_64 ]; then
  for SIBLING in ../loxcarts/.cache/valheim-server ../beeswax-coating/.cache/valheim-server; do
    if [ -f "$SIBLING/valheim_server.x86_64" ]; then
      echo "Seeding .cache/valheim-server from $SIBLING"
      mkdir -p .cache/valheim-server
      rsync -a --delete "$SIBLING/" .cache/valheim-server/
      break
    fi
  done
fi

# steamcmd update checks intermittently time out on Valve's CDN (exit 8,
# state 0x6). Retry a few times; if the cache was seeded from an
# already-validated install we proceed with a warning rather than blocking.
steam_ok=0
for attempt in 1 2 3; do
  if docker run --rm \
      --user "$UID_GID" \
      -e HOME=/tmp/home \
      -v "$PWD:/src" \
      beeswax-build '
        /opt/steamcmd/steamcmd.sh \
          +force_install_dir /src/.cache/valheim-server \
          +login anonymous \
          +app_update 896660 validate \
          +quit
      '; then
    steam_ok=1
    break
  fi
  echo "steamcmd attempt $attempt failed - retrying" >&2
  sleep 5
done

if [ "$steam_ok" -ne 1 ]; then
  if [ -f .cache/valheim-server/valheim_server.x86_64 ] \
     && [ -f .cache/valheim-server/steamapps/appmanifest_896660.acf ]; then
    echo "WARNING: steamcmd validation failed after retries; using the existing" >&2
    echo "cached install ($(grep -oP '(?<=\"buildid\"\t\t\")[0-9]+' .cache/valheim-server/steamapps/appmanifest_896660.acf | head -1)). Re-run later if the game just patched." >&2
  else
    echo "ERROR: no validated server install available" >&2
    exit 1
  fi
fi

docker run --rm \
  --user "$UID_GID" \
  -e HOME=/tmp/home \
  -v "$PWD:/src" \
  beeswax-build '
    set -e
    cd /src/.cache/valheim-server/valheim_server_Data/Managed
    mkdir -p /src/refs
    cp assembly_valheim.dll assembly_utils.dll assembly_guiutils.dll \
       SoftReferenceableAssets.dll netstandard.dll /src/refs/
    cp Assembly-CSharp*.dll /src/refs/ || true
    cp UnityEngine*.dll /src/refs/
    ls -la /src/refs/
  '
