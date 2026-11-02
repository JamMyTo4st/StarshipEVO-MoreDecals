# Starship EVO More Decals

A BepInEx 5 mod for Starship EVO that lets you place your own PNG images as decals.

> **Tested in game:** November 2026, Starship EVO build 26w43a.

## What it does

- Loads the PNG files you put in `BepInEx\plugins\MoreDecals\Custom\` and lets you place them as decals.
- Adds no new brick ids. A custom decal is saved as one of the normal FACTION #1-#4 decals, so ships and blueprints still load for players without the mod (they see the FACTION decal instead).
- Comes with no built-in decals. If the `Custom` folder is empty, the picker says so.

## Controls

1. Press **Minus** (`-`) to open the More Decals picker and click a decal. Pick **None** to go back to normal FACTION decals.
2. Select any **Codex > Decals > FACTION #1-#4** decal and place it as usual. It shows your chosen image once built.

## Requirements

- Windows.
- Starship EVO on Steam (app 711980).
- [BepInEx 5.4.x](https://github.com/BepInEx/BepInEx/releases), x64 build. BepInEx 6 does not work.

## Install

1. Install BepInEx 5.4.x x64 into the game folder (the folder with `StarshipEVO.exe`) and start the game once, then close it.
2. Download `MoreDecals-v1.0.0.zip` from the Releases page.
3. Create the folder `BepInEx\plugins\MoreDecals\` and extract the zip into it. You should have `BepInEx\plugins\MoreDecals\MoreDecals.dll` and `BepInEx\plugins\MoreDecals\Custom\`.
4. Put your PNG files into `BepInEx\plugins\MoreDecals\Custom\`. Square 256x256 with a white shape on a transparent background works best (it takes the paint color).
5. Start the game.

## Check that it works

`BepInEx\LogOutput.log` should contain lines like:

```
[Custom] loaded 01_logo.png -> id 1.
MoreDecals 1.0.0 loaded, waiting for game resources. ...
[Start] game resources ready, patched 4 methods.
[Inject-B] OK: copied N glyphs in place into MAIN_TEX layers [...]
```

If you see `[Inject-B] ABORTED ... Reason: ...`, your decals show the FACTION placeholder. Please report that line.

## Configuration

The file is created on first start: `BepInEx\config\com.jammytoast.starshipevo.moredecals.cfg`

```ini
[Decals]
Enabled = true
PickerKey = Minus
InjectTextures = true

[Debug]
VerboseLogging = true
```

| Section | Key | Default | Meaning |
|---|---|---|---|
| Decals | `Enabled` | `true` | Off: the mod does nothing; custom decals show the FACTION placeholder. |
| Decals | `PickerKey` | `Minus` | Key that opens and closes the picker. |
| Decals | `InjectTextures` | `true` | Adds your images to the game's textures after a self-check. Off: textures are not touched. |
| Debug | `VerboseLogging` | `true` | Writes extra details to the log. |

## Uninstall

- **Mod only:** delete `BepInEx\plugins\MoreDecals\` and `BepInEx\config\com.jammytoast.starshipevo.moredecals.cfg`. Placed decals turn back into FACTION decals.
- **BepInEx fully:** also delete the `BepInEx` folder, `winhttp.dll`, `doorstop_config.ini` and `.doorstop_version` (if present) from the game folder.

## Notes / Game updates

- **18 decals max.** Images go into free texture layers in the game, and there are currently 18. Extra files are ignored and listed as a warning in the log.
- **File order matters.** Files are used in file-name order, and a ship stores only the decal's number. Adding, removing or renaming a file that sorts earlier changes what already placed decals show. Use names like `01_logo.png`, `02_arrow.png` and add new files at the end.
- **Other players need the same PNGs** with the same names to see your decals. Otherwise they see the FACTION decal.
- Back up your saves. A game update can break the mod; check the log after updates.

## Building from source

Requires the .NET SDK and a copy of the game.

```
dotnet build -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Starship EVO"
```

The output is `bin\Release\netstandard2.0\MoreDecals.dll`. Game DLLs are referenced from `GameDir` and are not part of this repository.

## Changelog

- **1.0.0**: First public release. Custom PNGs only (no built-in decals), up to 18, in file-name order. Picker opens with Minus.
- **0.3.0**: Removed built-in glyphs; custom PNGs only, resized to 256x256.
- **0.2.0**: Images copied into unused texture layers.
- **0.1.x**: Load-order and rendering fixes.

## Reporting issues

Open an issue on GitHub and attach `BepInEx\LogOutput.log` from a session where the problem happened.

## License

MIT (c) 2026 JamMyTo4st. See `LICENSE`.
