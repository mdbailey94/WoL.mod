# Custom Paintings (Wizard of Legend)

Hang your own pictures in the Chaos Trials. Some of the breakable paintings on the trial walls
show one of your pictures instead, fitted inside the game's own frame and shrunk to the
painting's size so it stays crisp pixel art.

## Install

Download `WoLCustomPaintings.zip` from the
[latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it into
your Wizard of Legend folder. Needs BepInEx only.

## Adding pictures

Put PNG files in `BepInEx\plugins\WoLCustomPaintings\Paintings\` (the folder is created the
first time the game runs with the mod). Each painting that gets swapped picks one at random.

For the best look, use a **pixel-art picture about 64 pixels wide with up to 64 colours**: the mod
then snaps the shrunk picture back to those colours, so it matches the game's style. Any other
PNG works too; it's just shrunk and kept as is. The top of the picture is kept when it's
cropped to the frame's shape, since that's usually where faces are.

## Settings

`BepInEx\config\mdbailey94.wol.custompaintings.cfg`, or the Mods menu for on/off:

- `Enabled`: on or off.
- `Chance`: share of paintings that show one of your pictures. Default `0.35`.
- `FrameInset`: how many pixels of the game's frame to keep around your picture. `0` (default)
  picks about 16% of the painting's size; raise it if your picture covers the frame, lower it if
  the old canvas peeks out.

`BepInEx\LogOutput.log` lists each picture loaded and each painting framed (with its size), which
helps when tuning `FrameInset`.
