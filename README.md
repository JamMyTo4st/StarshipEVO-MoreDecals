# Starship EVO More Decals

A BepInEx 5 mod for Starship EVO that lets you place your own PNG images as decals.

> **Tested in game:** v1.1.0 on game build 26w43a, November 2026.

## What it does

- Loads the PNG files from `BepInEx\plugins\MoreDecals\Custom` (up to 18).
- Adds them to the **Codex > Decals** tab as a **Custom Decals** family, each with its own icon and name. Drag one to the hotbar and place it like any other decal.
- Placed decals are still saved as Codex Decals > FACTION #1-#4 decals. No new brick ids are added, so saves and blueprints still load for players without the mod; they see the FACTION placeholder instead.
- A white shape on a transparent background takes the paint color like vanilla decals.
- Ships with one sample decal (`Custom\01_test-decal.png`).

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
[Custom] loaded 01_test-decal.png -> id 1.
MoreDecals 1.1.0 loaded, waiting for game resources. ...
[Start] game resources ready, patched 4 methods.
[Inject-B] OK: copied N glyphs in place into MAIN_TEX layers [...]
[Codex] added family 'Custom Decals' with N entries to the Decals tab ...
```

If you see `[Inject-B] ABORTED ... Reason: ...`, custom decals show the FACTION placeholder. Please report that line.

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
| Debug | `VerboseLogging` | `true` | Logs extra details to `LogOutput.log`. |

## Uninstall

- **Mod only:** delete `BepInEx\plugins\MoreDecals\` and `BepInEx\config\com.jammytoast.starshipevo.moredecals.cfg`. Placed custom decals turn back into FACTION decals.
- **BepInEx fully:** also delete the `BepInEx` folder, `winhttp.dll`, `doorstop_config.ini` and `.doorstop_version` (if present) from the game folder.

## Notes / Game updates

- **18 decals maximum.** Each image uses one free layer in the game's block texture array, and the current game has 18. Extra files are ignored and logged as a warning.
- **File order matters.** A placed decal stores its number (its position in file-name order), not the image. Adding, removing or renaming a file that sorts earlier changes what already placed decals show. Use prefixes like `01_logo.png`, `02_arrow.png` and add new files at the end.
- **Other players need the same PNGs** with the same names in the same order to see them; otherwise they see the FACTION decal.
- **The sample decal can be deleted** (or replaced). If you delete it, your own files move up one number, so do it before placing decals.
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

- **1.1.0**: Custom decals appear as Codex entries (Custom Decals family) with their own hotbar icons. Custom decals no longer stack with vanilla faction decals. Removed the status line that overlapped the HUD.
- **1.0.0**: First public release. Custom PNG decals only, up to 18. Ships with a sample decal. Picker key is now Minus.
- **0.3.0**: Removed built-in glyphs; only custom PNGs, in file-name order, with an empty-state picker.
- **0.2.0**: Images copied in place into unused `MAIN_TEX` layers.
- **0.1.x**: Development builds.

## License

MIT License, Copyright (c) 2026 JamMyTo4st. See `LICENSE`.
