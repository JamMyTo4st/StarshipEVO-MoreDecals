# Starship EVO More Decals

A BepInEx 5 mod for Starship EVO that lets you place your own PNG images as decals. It ships with 36 letters and numbers (A-Z, 0-9) as ready-made PNGs.

> **Tested in game:** v1.2.0 on game build 26w43a, November 2026. v1.3.0 only changes the shipped files (same code as 1.2.0).

## What it does

- Loads the PNG files from `BepInEx\plugins\MoreDecals\Custom` (up to 60) and adds them to the **Codex > Decals** tab as a **Custom Decals** family, each with its own icon and name.
- Ships with 36 PNGs in `Custom`: **A-Z and 0-9** in the Orbitron font (`001_A.png` ... `026_Z.png`, `027_0.png` ... `036_9.png`). They are normal custom files: delete or replace any of them. That leaves 24 slots for your own files (60 if you delete them). Drag one to the hotbar and place it like any other decal.
- Placed decals are still saved as Codex Decals > FACTION #1-#4 decals. No new brick ids are added, so saves and blueprints still load for players without the mod; they see the FACTION placeholder instead.
- A white shape on a transparent background takes the paint color like vanilla decals.

## Controls

1. Open the **Codex > Decals** tab and find the **Custom Decals** family.
2. Drag a decal to the hotbar, select it and place it as usual.

| Key | Action |
|---|---|
| **Minus (-)** | Fallback: open or close the More Decals picker window (config `PickerKey`). |

## Requirements

- Windows.
- Starship EVO on Steam (app 711980).
- [BepInEx 5.4.x x64](https://github.com/BepInEx/BepInEx/releases). BepInEx 6 is not supported.

## Install

1. Install BepInEx 5.4.x x64 into the game folder (`Steam\steamapps\common\Starship EVO`) and start the game once so BepInEx creates its folders, then close it.
2. Create the folder `BepInEx\plugins\MoreDecals\`.
3. Extract `MoreDecals-v1.1.0.zip` into that folder. You should have `BepInEx\plugins\MoreDecals\MoreDecals.dll` and `BepInEx\plugins\MoreDecals\Custom\`.
4. Put your own PNG files in `Custom` (square 256x256 recommended; other sizes are resized).
5. Start the game.

## Check that it works

`BepInEx\LogOutput.log` should contain lines like:

```
[Custom] loaded 001_A.png -> id 1.
...
[Custom] loaded 036_9.png -> id 36.
MoreDecals 1.3.0 loaded, GrowTexture=True ... custom=36 ...
[Grow] OK: VoxelDiffuse depth 403 -> 439 (+36), swapped before VOXEL_DIFFUSE/MAIN_TEX were set.
[Start] game resources ready, patched 4 methods.
[Grow] using grown layers; fallback not used.
[Codex] added family 'Custom Decals' with 36 entries to the Decals tab ...
```

If you see `[Grow] fallback used`, only the first 18 files get images. If you see `[Inject-B] ABORTED ... Reason: ...`, the decals show the FACTION placeholder. Please report that line.

## Configuration

File: `BepInEx\config\com.jammytoast.starshipevo.moredecals.cfg` (created on first start).

```ini
[Decals]
Enabled = true
PickerKey = Minus
UseCodexEntries = true
ShowSelectionHint = false
InjectTextures = true

[Debug]
VerboseLogging = true
```

| Section | Key | Default | Meaning |
|---|---|---|---|
| Decals | `Enabled` | `true` | Off: no patches; custom decals show the FACTION placeholder. |
| Decals | `UseCodexEntries` | `true` | Shows the custom decals in the Codex Decals tab. Off: use the picker key. |
| Decals | `PickerKey` | `Minus` | Key that opens and closes the fallback picker (Unity KeyCode name). |
| Decals | `ShowSelectionHint` | `false` | Shows a small hint at the top of the screen with the decal picked in the picker. |
| Decals | `InjectTextures` | `true` | Adds the images to the game's block textures after a self-check. Off: textures are not touched. |
| Decals | `GrowTexture` | `true` | Create the block texture array with extra layers for your PNGs (lifts the 18 limit). Off: 1.1 behaviour. |
| Decals | `MaxExtraLayers` | `128` | Maximum extra layers `GrowTexture` may add. |
| Debug | `VerboseLogging` | `true` | Logs extra details to `LogOutput.log`. |

## Uninstall

- **Mod only:** delete `BepInEx\plugins\MoreDecals\` and `BepInEx\config\com.jammytoast.starshipevo.moredecals.cfg`. Placed custom decals turn back into FACTION decals.
- **BepInEx fully:** also delete the `BepInEx` folder, `winhttp.dll`, `doorstop_config.ini` and `.doorstop_version` (if present) from the game folder.

## Notes / Game updates

- **Multiplayer.** Decals are saved as vanilla FACTION #1-#4 decals plus a slot number, so nothing new is sent over the network and players without the mod just see the FACTION placeholder. Everyone must have the same PNGs with the same file names (same order) in `Custom` to see the same decals; a different set or order shows whatever image is at that slot number. Share your `Custom` folder as a pack with your group. The extra texture layers are created locally on each PC.
- **60 decals maximum.** The save format stores 4 bricks x 15 slots = 60. With the 36 shipped letters/numbers that leaves 24 for your own PNGs. Extra PNGs are ignored with a warning. Since 1.2.0 the game's block texture array is created with one extra layer per PNG (`GrowTexture`, capped by `MaxExtraLayers`). If that fails, the mod falls back to the 1.1 method (the 18 free layers) and logs `[Grow] fallback used`.
- **File order matters.** A placed custom decal stores its number (its position in file-name order), not the image. Adding, removing or renaming a file that sorts earlier changes what already placed decals show. The shipped files all start with `0`, so **start your own names with `1` or higher** (e.g. `100_logo.png`, `101_arrow.png`; names starting with a letter also sort after them) and add new files at the end. To drop the letters/numbers, delete them before placing decals.
- **Other players need the same PNGs** with the same names in the same order to see them; otherwise they see the FACTION decal.
- Game updates may break the mod. Back up your saves.

## Building from source

Requires the .NET SDK. Game DLLs are not included; point `GameDir` at your game install:

```
dotnet build -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Starship EVO"
```

The output is `bin\Release\netstandard2.0\MoreDecals.dll`.

## Reporting issues

Open an issue on GitHub with what you did, what happened, and attach `BepInEx\LogOutput.log`.

## Changelog

- **1.3.0**: Ships 36 Orbitron letter/number PNGs (A-Z, 0-9, same height and baseline) in `Custom` (`001_A`...`036_9`), replacing the sample decal; documented naming scheme for your own files. GPU/texture diagnostics only with `VerboseLogging`.
- **1.2.0**: Up to 60 decals: the game's block texture array is created with extra layers at load time (falls back to the 18 free layers). New options `GrowTexture`, `MaxExtraLayers`. Multiplayer notes.
- **1.1.0**: Custom decals appear as Codex entries (Custom Decals family) with their own hotbar icons. Custom decals no longer stack with vanilla faction decals. Removed the status line that overlapped the HUD.
- **1.0.0**: First public release. Custom PNG decals only, up to 18. Ships with a sample decal. Picker key is now Minus.
- **0.3.0**: Removed built-in glyphs; only custom PNGs, in file-name order, with an empty-state picker.
- **0.2.0**: Images copied in place into unused `MAIN_TEX` layers.
- **0.1.x**: Development builds.

## License

MIT License, Copyright (c) 2026 JamMyTo4st. See `LICENSE`.

## Credits

The shipped letter and number PNGs use **Orbitron** by Matt McInerney (The Orbitron Project Authors), licensed under the SIL Open Font License 1.1. See `OFL-Orbitron.txt`.
