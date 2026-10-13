# LoxPlate

Armor barding for your tamed loxes. Two late-game sets turn your beasts from
raid fodder into armored transports — because the animal hauling your wagon
across the plains deserves plate armor more than you do.

Barding adds two layers of protection to a tamed lox: a flat percentage of all
incoming damage is ignored, and whatever gets through is absorbed by a bonus
health pool before the lox itself is hurt. The pool refills on its own while
the lox stays out of combat. Damage the pool soaks shows up as grey
immune-style numbers, and aiming at a barded lox shows its remaining pool, so
you can always tell how much armor it has left.

| | Black Metal Barding | Flametal Barding |
| --- | --- | --- |
| Damage reduction | 25% | 35% |
| Health pool | 300 | 600 |
| Movement penalty | −5% | −5% |
| Crafted at | Forge (level 2) | Forge (level 3) |
| Recipe | 20 Black Metal, 12 Lox Pelt, 10 Linen Thread, 1 Fuling Totem | 16 Flametal, 16 Lox Pelt, 20 Linen Thread, 1 Warrior Trophy |

The Fuling Totem and the Charred Warrior's trophy make each set a deliberate
prize — barding is meant to protect loxes you have already invested in.

## Fitting and removing

1. Craft the barding at a forge.
2. Select it in your hotbar, aim at your **tamed** lox and press **Use**.
3. To remove it, crouch and press **Use** while aiming at the barded lox —
   the set drops to the ground for you to pick up.

Note that tamed animals are immune to player attacks while calm — that is
vanilla behavior, and it includes the butchering knife's exceptions. Barding
protects against **enemy** damage (raids, fulings, deathsquitos); to see it
work, let a fuling take a swing at the lox, or use the butchering knife
(`KnifeButcher`), the one player weapon that can strike a tame.

**Wear and repair:** every hit the barding's pool absorbs wears the set down
(1 durability per absorbed hit by default). Worn-out barding keeps its flat
damage reduction but stops absorbing. Take the set off (crouch + Use) and
repair it at any forge like ordinary armor — the vanilla repair tab restores
it. Aiming at a barded lox shows the set's remaining durability alongside the
pool. Barding never degrades while fitted and unworn; `DurabilityPerAbsorb = 0`
in the config disables wear entirely.

Each lox wears one set at a time. Fitting a different tier returns the old one.
When a barded lox dies, its barding drops with the corpse (configurable).
Barding is fitted per animal and persists through save/load; the armor state
syncs to everyone on the server.

The visual shell is built in-game from Valheim's own materials — black metal
plate for the first tier, smoldering flametal plate with ember edges for the
second. It carries no collision, so riding, hitching (see LoxCarts) and combat
behave exactly as before. Want your own art? Drop a custom
`loxplate.assets` AssetBundle next to `LoxPlate.dll` and it replaces the
procedural shells — see `BUNDLING.md` in the repo for the full pipeline.

## Configuration

Close the game before editing `BepInEx/config/benjamin.loxplate.cfg`, then
restart. All values apply to both sets unless noted.

| Setting | Default | Effect |
| --- | --- | --- |
| `[General] AllowAnyTame` | `false` | Let wolves, boars and other tames wear barding too. |
| `[General] RecoverOnDeath` | `true` | Barding drops when its wearer dies instead of being lost. |
| `[BlackMetalBarding] DamageReduction` | `0.25` | Fraction of damage ignored (0–0.9). |
| `[BlackMetalBarding] HealthPool` | `300` | Absorb pool (0–5000). |
| `[BlackMetalBarding] SpeedPenalty` | `0.05` | Movement penalty (0–0.5). |
| `[FlametalBarding] DamageReduction` | `0.35` | Fraction of damage ignored (0–0.9). |
| `[FlametalBarding] HealthPool` | `600` | Absorb pool (0–5000). |
| `[FlametalBarding] SpeedPenalty` | `0.05` | Movement penalty (0–0.5). |
| `[Pool] RegenPerSecond` | `5` | Pool refill per second out of combat (0–100). |
| `[Pool] RegenDelaySeconds` | `30` | Seconds unharmed before the pool refills (0–600). |
| `[Barding] DurabilityPerAbsorb` | `1` | Durability lost per absorbed hit; 0 disables wear (0–10). |

## Installation

Install via r2modman, Gale or Thunderstore Mod Manager
(`Stew_and_Brew_Labs-LoxPlate`), including these dependencies:

- BepInExPack Valheim 5.4.2351
- Jötunn 2.30.2

Manual installation: install the dependencies and place `LoxPlate.dll` in
`Valheim/BepInEx/plugins/`.

## Current limitations

- Multiplayer is unverified: like most Valheim mods, damage protection is
  applied on whichever client delivers a hit, so simultaneous shooters may
  drain the pool slightly out of step. Single-player and co-op behavior
  should feel identical.
- The barding shell does not follow the lox's fur animation; it is a fitted
  plate harness, not a skin.
- Barding only protects the animal wearing it — riders still wear their own
  armor.

## Credits

- LoxPlate is an original mod by benjamin
  ([Stew_and_Brew_Labs](https://thunderstore.io/c/valheim/p/Stew_and_Brew_Labs/)).
- Uses Valheim's models and materials at runtime. No game assemblies or
  textures are distributed.
- Companion mod: [LoxCarts](https://thunderstore.io/c/valheim/p/Stew_and_Brew_Labs/LoxCarts/),
  lox-drawn wagons — armor the lox, then give it something to pull.
