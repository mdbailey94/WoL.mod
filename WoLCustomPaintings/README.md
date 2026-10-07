# Custom Paintings (Wizard of Legend)

Hang your own pictures in the Chaos Trials. Now and then (0.5% by default) a breakable painting
on the trial walls shows one of your pictures instead of its artwork, inside the game's own frame,
shrunk to fit so it stays crisp pixel art.

## Install

Download `WoLCustomPaintings.zip` from the
[latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it into
your Wizard of Legend folder. Needs BepInEx only.

## Adding pictures

Put PNG or JPG files in `BepInEx\plugins\WoLCustomPaintings\Paintings\` (the folder is created the
first time the game runs with the mod), or in folders inside it. Each painting that gets swapped
picks one at random. Pictures added while the game is running are picked up on the next floor.
Other kinds of pictures (WEBP, HEIC from a phone, GIF...) don't work: save them as PNG or JPG
first. The log names every file it skips, and why.

For the best look, use a **pixel-art picture with up to 64 colours**, ideally about as tall as it
is wide or taller (the frames are portrait-shaped, roughly 3:4): the mod then snaps the shrunk
picture back to those colours, so it matches the game's style. Any other PNG or JPG works too;
it's just shrunk and kept as is. Each picture's own size is read and it's scaled to the frame
(see `PictureFit`).

## Settings

`BepInEx\config\mdbailey94.wol.custompaintings.cfg`, or the Mods menu for on/off:

- `Enabled`: on or off.
- `ChancePercent`: percent of paintings that show one of your pictures. Default `0.5`, so
  they're a rare find; raise it (up to `100`) to see them more often. A change counts from the
  next floor you enter, no restart needed.
- `Detail`: how finely your picture is drawn inside the frame. `1` is the game's own pixel size
  (blockier, matches the game's paintings); `2` (default) to `4` fit more detail in the same space.
- `PictureFit`: how your picture is fitted to the frame. `Fit` (default) shows the whole picture,
  scaled to fit, on a dark mat where its shape differs from the frame's; `Fill` fills the frame,
  cropping the sides (or the bottom); `Stretch` stretches the whole picture to the frame's shape.
- `FrameBorder`: how many of the game's pixels of the frame show round your picture (default 2).
  The picture fills the frame's opening whatever the painting's size; raise this if it covers
  the frame, lower it if the old painting peeks out. Applies from the next floor.
- `NudgeUp`: moves your picture up inside the frame by this many of the game's pixels (negative
  moves it down; the artwork is about 42 pixels tall). Applies from the next floor.
- `FrameInset`: only for a painting without a separate artwork layer: how many pixels of its
  frame to keep around your picture (`0` = automatic).

`BepInEx\LogOutput.log` lists each picture loaded, the chance in use, each painting's artwork
replaced (with its size), the layers of the first painting and, on leaving each floor, how many of its
paintings showed your pictures. If it says `No pictures yet`, it also shows the folder it looked
in.
