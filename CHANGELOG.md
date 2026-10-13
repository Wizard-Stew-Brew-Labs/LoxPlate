# Changelog

## 0.1.0

Initial release.

- Black Metal Barding and Flametal Barding, crafted at a forge (Fuling Totem
  and Charred Warrior Trophy as the prestige ingredients respectively).
- Fitting via Use on a tamed lox; removal via crouch + Use.
- Removing (or replacing) a barding returns it to the removing player's
  inventory when there is room (durability preserved); otherwise it drops at
  the lox's feet.
- Protection model: flat damage reduction applied to every hit, then an
  absorbing health pool with out-of-combat regeneration. Absorbed damage
  displays as immune-style numbers; aiming at a barded lox shows its pool and
  remaining durability.
- Durability: absorbed hits wear the set (1 per hit, configurable); worn-out
  barding keeps its reduction but stops absorbing. Removed or dropped barding
  carries its wear and is repaired with the vanilla forge repair tab.
- Removal via crouch + Use works both empty-handed (Interact path) and with
  an item selected (UseItem path); Alt+Use rename keeps vanilla precedence.
- Movement speed penalty while barded (configurable).
- Barding state persists on the animal's ZDO across save/load and syncs to
  all peers; dropped on death (configurable, recoverable).
- Procedural plate-shell visuals built from vanilla materials; no shipped art.
- Contract test suite pinning every game API and identity string the mod
  depends on.
