MoreDecals - custom decals
==========================

Drop your PNG files in this folder (BepInEx\plugins\MoreDecals\Custom) and restart the game.
Then press Minus (-) in game to pick one and build with any FACTION #1-#4 decal.

- Maximum 18 images (one per free texture slot). They are loaded in file-name order;
  extra files are ignored and a warning is written to BepInEx\LogOutput.log.
- Square 256x256 PNG recommended. Other sizes are resized to 256x256 (non-square = stretched).
- Use a WHITE image on a TRANSPARENT background so it takes the paint color.
  Colored images are multiplied by the paint color.
- Placed decals remember their number (position in file-name order), not the image.
  Name files 01_name.png, 02_name.png, ... and add new ones at the end so placed decals
  do not change. Other players need the same files with the same names.
