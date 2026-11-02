# Starship EVO More Decals

A BepInEx 5 mod that lets you place **your own PNG decals** in Starship EVO. It adds **no new brick ids**: a modded decal is saved as one of the vanilla FACTION #1-#4 decals, so saves and blueprints still load for players without the mod.

v0.3.0 has **no built-in decals**: only the PNGs you put in the `Custom` folder are offered. Back up your saves first; game updates may break the mod.

## How to use

1. Put PNG files in `BepInEx/plugins/MoreDecals/Custom/` and start the game.
2. Press **F8** (config `PickerKey`) to open the More Decals window and click a decal. Pick **None** to go back to normal faction decals. With no PNGs the window says so.
3. Select any **Codex > Decals > FACTION #1-#4** decal and build as usual. While a modded decal is selected, a line at the bottom of the screen shows its name.

## Your own decals

- **Format:** PNG, square **256x256** RGBA recommended. Any other size is resized to 256x256 (non-square images are stretched). Transparency is kept. A **white shape on a transparent background** takes the paint color like vanilla decals; a colored image is multiplied by the paint color.
- **How many:** one per free texture slot in the game, currently **18**. Files are taken in file-name order; extra files are ignored and listed as a warning in `BepInEx/LogOutput.log`.
- **Order matters:** the ship only stores the decal's number (its position in file-name order), not the image. Adding, removing or renaming a file that sorts earlier changes which image already placed decals show. Prefix names like `01_logo.png`, `02_arrow.png` and only add new files at the end.
- **Other players need the same files with the same names** to see them; otherwise they see the FACTION placeholder.

## What vanilla players (and players without the file) see

A modded decal is a normal **FACTION #1-#4 decal brick**, so they see that FACTION decal in the same place, size, rotation and color. Nothing crashes, and the image comes back when the ship is opened again with the mod.

## How it works (technical)

- **Storage:** the top 4 bits of `BrickInstanceData.scale` (saved and synced by the game) on the 4 FACTION bricks (ids 309-312): up to 60 ids.
- **Rendering:** after the game has loaded, the decal images are encoded to the voxel diffuse array's format (DXT5 256x256, full mip chain) and GPU-copied **in place** into `MAIN_TEX` layers that no material references. The `MAIN_TEX` object is never replaced. A postfix on `DecalRendererResources.SetDecal` points modded decals at their layer.
- **Placing:** a prefix on `ActionBuild.LeftClickUp` writes the brick id and decal bits. A patch on `EntitySymmetry.ApplySymmetry` keeps them on mirrored copies.

## Config (`BepInEx/config/com.jammytoast.starshipevo.moredecals.cfg`)

| Section | Key | Default | Meaning |
|---|---|---|---|
| Decals | `Enabled` | `true` | Off: no patches; modded decals show the FACTION placeholder. |
| Decals | `PickerKey` | `F8` | Opens and closes the picker. |
| Decals | `InjectTextures` | `true` | Adds the decal images to the game's block textures, only after a strict self-check. Off: textures are never touched; modded decals show the FACTION placeholder. |
| Debug | `VerboseLogging` | `true` | Logs extra details while placing decals. |

## Requirements and install

- Starship EVO (Steam) and [BepInEx 5.4.x](https://github.com/BepInEx/BepInEx/releases) (x64).
- Extract `MoreDecals-v0.3.0.zip` into `BepInEx/plugins/MoreDecals/` (create the folder), so you get `BepInEx/plugins/MoreDecals/MoreDecals.dll` and `BepInEx/plugins/MoreDecals/Custom/`.

## Check that it works

`BepInEx/LogOutput.log` should contain:

```
[Custom] loaded mydecal.png -> id 1.
MoreDecals 0.3.0 loaded, waiting for game resources. ...
[Start] game resources ready, patched 4 methods.
[Inject-B] OK: copied N glyphs in place into MAIN_TEX layers [...]
```

If you see `[Inject-B] ABORTED ... Reason: ...`, modded decals keep the FACTION placeholder. Please report that line.

## Changelog

- **0.3.0**: Removed all built-in glyphs (and the Nunito font). Only custom PNGs from the `Custom` folder are loaded, in file-name order, up to the number of free texture layers (18); extras are logged. Images of any size are resized to 256x256 with alpha-aware filtering. The picker shows an empty-state message when there are none.
- **0.2.0**: Glyphs copied in place into unreferenced `MAIN_TEX` layers (no new texture array).
- **0.1.4**: Fixed blocks and vanilla decals rendering dark and flat when `InjectTextures` is on. The enlarged texture array is now fully uploaded from its CPU copy, and the self-check compares colour and transparency at full resolution (mips 0, 1, 4); any mismatch leaves the world textures vanilla.
- **0.1.3**: Fixed the "void" world again. v0.1.2 applied its patches while the game was still loading its asset repos, which broke the game's help-message and icon setup (`UI_HelpMessagesRepo` TypeInitializationException) so the world never finished loading. MoreDecals now waits until every asset repo, including the icon mapping, has loaded.
- **0.1.2**: v0.1.1 never got past "waiting for game resources" (the BepInEx manager object gets no Update() in this game), so F8 and placing did nothing. Polling now runs on its own hidden persistent object plus a scene-loaded callback, logs what it is waiting for every 10 s, and logs poller exceptions. Added `[Picker]` and `[Place]` log lines.

- **0.1.1**: Fixed loading a save into an empty "void" world. v0.1.0 patched `ResourcesLoader.Awake`. That made the game build its schematics list before its item data existed (`TabSchematics` NullReferenceException). Patches are now applied only after the game has loaded. The texture step now runs a self-check and aborts safely (`InjectTextures` switch). The game's `VOXEL_DIFFUSE` array is no longer replaced. Placing decals has a fallback if the build patch fails to compile.
- **0.1.0**: First build.

## Known limitations

- The picker is a separate F8 window, not a new section in the Codex Decals tab.
- The decal preview while placing shows the FACTION placeholder; the glyph shows once it is built.
- Clicks in the picker window may also reach the game behind it.

## Credits and license

- Mod code: MIT License, Copyright (c) 2026 JamMyTo4st (see `LICENSE`).
