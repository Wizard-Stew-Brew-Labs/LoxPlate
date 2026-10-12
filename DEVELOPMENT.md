# LoxPlate — development notes

Build, test and server tooling for LoxPlate. The player-facing README is `README.md`.

## Approach

LoxPlate is an original mod (no upstream). It follows the same containerized
toolchain and conventions as the sibling `loxcarts` project: the shared
`beeswax-build` docker image (dotnet 8 + steamcmd + ilspycmd), refs staged
from a steamcmd-validated dedicated-server install, and libs pinned to the
versions the shipped manifest declares.

## Toolchain

Docker required; every script runs its work inside `beeswax-build`:

```sh
scripts/image.sh        # ensure the toolchain image exists (reuses the shared one)
scripts/fetch-refs.sh   # game reference assemblies -> refs/ (seeds .cache from loxcarts, steamcmd-validated)
scripts/fetch-libs.sh   # BepInEx 5.4.2351 + Jotunn 2.30.2 -> libs/
scripts/build.sh        # compile src/LoxPlate.csproj
scripts/test.sh         # unit + contract tests
scripts/package.sh      # dist/LoxPlate-<version>.zip
scripts/check-package.sh
scripts/deploy.sh       # push dist zip into the running valheim-loxcarts container + restart
python3 scripts/make-icon.py   # regenerate packaging/icon.png (deterministic)
```

## Architecture

- `src/LoxPlatePlugin.cs` — entry, config, and every Harmony patch:
  - `Tameable.UseItem` prefix: fit (barding selected + Use) / remove (crouch + Use)
  - `Tameable.Awake` postfix: attaches `LoxBarding` to tameables
  - `Character.ApplyDamage` prefix: reduction then pool absorption, server of the
    hit applies it (same trust model as vanilla health writes)
  - `Character.GetJogSpeedFactor`/`GetRunSpeedFactor` postfixes: movement penalty
  - `Character.OnDeath` postfix: drops the barding (owner-side, configurable)
  - Registration hooks on `ZNetScene.Awake`, `ZNet.Start`, `ObjectDB.Awake`,
    `ObjectDB.CopyOtherDB` — all idempotent; dedicated servers never call
    `CopyOtherDB`, hence the `ZNet.Start` backstop.
- `src/BardingItems.cs` — clones the item prefabs from the vanilla `BlackMetal`
  pickup, registers them with `ZNetScene` (network drops) + `ObjectDB`
  (inventory) and adds the forge recipes. Recipes use `KeyValuePair<string,int>`
  — value tuples JIT-resolve to `System.ValueTuple`, which the game does not
  ship for net462 (a TypeLoadException at the call site, learned the hard way).
- `src/LoxBarding.cs` — per-animal component: ZDO state (`lb_tier`, `lb_pool`,
  `lb_lastHit` world-time key), the `lb_fit` RPC, lazy pool regeneration and
  protection application.
- `src/BardingVisuals.cs` / `src/BardingIcon.cs` — procedural plate shells from
  vanilla donor materials (BlackMetal / FlametalOre / LoxPelt pickups), no
  colliders, plus the offscreen item-icon render (WagonThumbnail technique).
- `src/BardingMath.cs` — Unity-free damage-routing math, compiled directly into
  the test project.

Prefab IDs to know: the Yagluth summon item is `GoblinTotem` (display name
"Fuling Totem" — there is no `FulingTotem` prefab), the forge station is the
ZNetScene piece `forge` (lowercase, not an ObjectDB item), and the Charred
Warrior trophy is `TrophyCharredMelee` (display name "Warrior Trophy").

## Testing

`scripts/test.sh` runs 66 tests:

- **`BardingStatsTests`** — the shipped `BardingMath.cs` compiled into the test
  assembly: absorption split, remainder factors, regen delay/rate/edges.
- **`GameContractTests`** — MetadataLoadContext over the compiled DLL plus the
  real 1.0+ game assemblies: every Harmony patch target, reflected member,
  ZDO accessor shape, item/recipe construction surface, plugin identity,
  saved-data strings (UTF-8 and UTF-16 metadata heaps both scanned) and
  packaging coherence. **A failure here means a game update broke the mod —
  re-verify before shipping.**

MLC gotchas baked into the helpers: reflected `Type` instances are not
reference-equal to test-runtime `typeof(...)`, so signatures compare by type
`Name`; assemblies load lazily, so `assembly_utils` is force-loaded before the
fallback type scan; `Utils` lives in `assembly_utils`, not `assembly_valheim`.

## Server testing

The mod deploys into the shared dev container (`valheim-loxcarts`) via
`scripts/deploy.sh` — LoxPlate coexists with LoxCarts (different GUIDs). The
container restarts on deploy; the world persists in its docker volume.

In-game validation checklist (needs a client): craft both barding at a forge →
`/spawn Lox 1 1` + tame → fit/remove with Use / crouch+Use → shoot the lox and
watch damage numbers against the configured reduction/pool → restart the server
and confirm the barding persists → kill the lox and pick the barding up.
