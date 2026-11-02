MoreDecals - custom decals
==========================

Drop your PNG files in this folder (BepInEx\plugins\MoreDecals\Custom) and restart the game.
They appear in the Codex > Decals tab as the "Custom Decals" family (or press Minus (-)
while holding a FACTION #1-#4 decal).

Shipped files: 36 letters and numbers in the Orbitron font (see OFL-Orbitron.txt):
  001_A.png ... 026_Z.png, then 027_0.png ... 036_9.png  -> decals #1-#36.
You can delete or replace them (delete them before placing decals, see below).

NAMING YOUR OWN FILES: files are sorted by name (case-insensitive). The shipped files all
start with "0", so start your names with "1" or higher, e.g. 100_logo.png, 101_arrow.png
(any name starting with a letter also sorts after them). That leaves 24 slots (#37-#60).

- Maximum 60 images (save format: 4 bricks x 15 slots). They are loaded in file-name order;
  extra files are ignored and a warning is written to BepInEx\LogOutput.log.
  (If GrowTexture fails the mod falls back to 18; see the log for [Grow] lines.)
- Square 256x256 PNG recommended. Other sizes are resized to 256x256 (non-square = stretched).
- Use a WHITE image on a TRANSPARENT background so it takes the paint color.
  Colored images are multiplied by the paint color.
- Placed decals remember their number (position in file-name order), not the image.
  Adding, deleting or renaming a file that sorts earlier shifts the numbers, so placed
  decals show a different image. Add new files at the end (e.g. 100_, 101_, 102_ ...). Other players need the same files with the same names.
