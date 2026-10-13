# Custom barding art — the AssetBundle pipeline

LoxPlate loads an optional Unity AssetBundle named `loxplate.assets` from its
install folder (next to `LoxPlate.dll`). When it contains a shell prefab for a
tier, that art replaces the procedural plates; when the bundle (or a tier's
prefab) is missing, the mod falls back to the built-in procedural shell.
Nothing breaks without the bundle — it is purely additive.

## 1. Editor version — use Unity 6000.0.66f2

Build bundles with **Unity 6000.0.66f2** — the exact editor used by
ValheimArmory, the reference custom-asset mod verified working on the 1.0.17
client. The acceptance window is razor-thin: 6000.0.61f1 and 6000.0.75f1
(public editor) builds are refused as "older runtime", 6000.0.84f1 also
refused, 6000.6.5f1 refused as "wrong version" (too new). The 1.0.17 client's
runtime is a hotfixed 6000.0.75 build whose internal revision sits between the
public .75f1 and .84f1 editors; only bundles built by an editor close below it
load. 6000.0.66f2 is the empirically proven choice (file **and** memory loads
were both tested on the client — same refusal, so the load path is irrelevant,
the editor revision is what matters).

- Editor: `~/Unity/6000.0.66f2/Editor/Unity`
  (tarball: `download.unity3d.com/download_unity/b20bc5da3050/...`)
- Bundle project: `~/Unity/LoxPlateBundle/` (pinned to 6000.0.66f2)
- Other installs on this machine (6000.0.61f1, 6000.0.75f1, 6000.0.84f1,
  Hub's 6000.6.5f1) produce refused bundles — don't use them.

First launch requires signing in with a Unity account and choosing the free
Personal license — do this once in the GUI.

## 2. Authoring

Project layout:

```
Assets/LoxPlate/Bundles/     <- everything in here goes into loxplate.assets
Assets/Editor/BuildLoxPlateBundle.cs   <- the build script (menu + CLI)
```

Author two shell prefabs, named exactly:

- `LoxPlate_BlackMetalBarding`
- `LoxPlate_FlametalBarding`

Authoring rules:

- **Body space:** build the shell with the origin at `(0, 2.2, 1.2)` in
  `Lox/Visual/offset` space, **+Z = lox forward, +Y = up**. Real lox bounds
  (from the rig dump): x −2..+2, y −0.16..+3.84 (back line ≈ 3.84), z −2..+5.
  The torso barrel is roughly x ±1.6, y 1.5..3.5, z −1..3; legs hang below and
  the neck extends toward +Z ~4..5.
- **No colliders needed:** the mod strips every collider from the instantiated
  shell at runtime, so the lox's own hitbox and player aim stay untouched.
  Author with colliders off (or don't add them).
- **Shaders:** use the **Standard** shader or **Legacy Shaders/Diffuse**.
  Custom/post-processing shaders will likely render magenta in-game (shader
  stripping). If a material shows magenta, switch it to Legacy Shaders/Diffuse
  and set the texture as the main texture.
- **Icon:** the mod renders the item icon from your shell automatically —
  keep the prefab reasonably centered around the origin so the shot frames it.

## 3. Building the bundle

In the editor: **LoxPlate → Build Asset Bundle**. Output lands in
`~/Unity/LoxPlateBundle/Build/AssetBundles/loxplate.assets`.

From a terminal (no GUI):

```sh
~/Unity/6000.0.75f1/Editor/Unity -batchmode -quit \
  -projectPath ~/Unity/LoxPlateBundle \
  -executeMethod LoxPlate.EditorTools.BuildLoxPlateBundle.BuildFromCommandLine
```

(The first batch-mode run needs an activated license; activate once via the
GUI first.)

## 4. Shipping it

- **Local install (Gale):** copy `loxplate.assets` into
  `BepInEx/plugins/Stew_and_Brew_Labs-LoxPlate/` next to `LoxPlate.dll`
  (create the folder if Gale flattened it).
- **Mod repo / Thunderstore:** copy it to `packaging/assets/loxplate.assets`
  in the repo — `scripts/package.sh` includes it in the zip next to the DLL,
  `check-package.sh` verifies it, and `scripts/deploy.sh` pushes it into the
  dev server container with everything else.

## 5. Verifying

- Fit a barding (`Use` on a tamed lox). The server/client log shows
  `Custom asset bundle loaded: loxplate.assets` when the bundle was picked up;
  the shell visually replaces the procedural plates on the next fit/world load.
- If you see the procedural plates instead: check the log for
  `No loxplate.assets found` (file not next to the DLL) or `could not be
  loaded (wrong Unity version?)` (bundle built with a mismatched editor).

## Troubleshooting

| Symptom | Cause / fix |
| --- | --- |
| Magenta/pink surfaces | Shader stripped — use Standard or Legacy Shaders/Diffuse |
| "not compatible with this newer version" / "not built with the right version or build target" | Bundle built with the wrong editor — rebuild with Unity 6000.0.66f2 |
| Bundle ignored, `No loxplate.assets found` | File isn't next to LoxPlate.dll — check the install folder |
| Shell buried inside the lox | Prefab wasn't authored in body space — re-center on the coordinates above |
| Shell floats behind the lox | Prefab authored facing −Z; the lox faces +Z |
| Shell ignores animation | The mod anchors the shell to the animated hip bone automatically — verify the prefab root has no negative/odd scale |
