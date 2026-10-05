# Mod Menu (Wizard of Legend)

Adds a **MODS** panel to the title screen with an on/off switch for each mod from this repo
(Camera Zoom, Rolling Gale, Extended Stats). Changes apply immediately, with no restart.

| Input | Action |
|-------|--------|
| **Select** (Back / View / Share / −), **M**, or click the MODS hint | Open the panel |
| Up / Down | Choose a mod |
| Left / Right, **A**, Enter or click | Turn it on/off |
| **B**, Select, M or Esc | Close |

While the panel is open, the title menu ignores input, so your presses only affect the panel.

What "off" means:
- **Camera Zoom:** normal camera, and the zoom hotkeys do nothing.
- **Extended Stats:** no info-box stats, overlay or post-run report.
- **Rolling Gale:** no longer offered in the arcana shop. If you already own it in a run, you keep it.

Each switch is the mod's own `[General] Enabled` setting, so you can also change it in that mod's
file in `BepInEx\config`.

## Install

Download `WoLModMenu.zip` from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest)
and unzip it into your Wizard of Legend folder. Only BepInEx is needed.
