# Custom barding art — the AssetBundle pipeline

LoxPlate loads an optional Unity AssetBundle named `loxplate.assets` from its
install folder (next to `LoxPlate.dll`). When it contains a shell prefab for a
tier, that art replaces the procedural plates; when the bundle (or a tier's
prefab) is missing, the mod falls back to the built-in procedural shell.
Nothing breaks without the bundle — it is purely additive.

## 1. Editor version — exact match required

Bundles must be built with **Unity 6000.0.75f1** (the engine Valheim 1.0 runs;
same version across 1.0.15–1.0.17 server builds). A bundle built with a
different Unity version may fail to load; the mod logs a warning and falls
back to procedural shells if so.

Installed locally at:

- Editor: `~/Unity/6000.0.75f1/Editor/Unity` (tarball from
  `download.unity3d.com/download_unity/26349cd2a5c8/LinuxEditorInstaller/`)
- Bundle project: `~/Unity/LoxPlateBundle/` (scaffolded, opens in the editor)

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
| Bundle ignored, warning about Unity version | Rebuild with exactly 6000.0.75f1 |
| Shell buried inside the lox | Prefab wasn't authored in body space — re-center on the coordinates above |
| Shell floats behind the lox | Prefab authored facing −Z; the lox faces +Z |
| Shell ignores animation | The mod anchors the shell to the animated hip bone automatically — verify the prefab root has no negative/odd scale |
