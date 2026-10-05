# Slingshot (Wizard of Legend)

A new **dash** arcana, added with [LegendAPI](https://github.com/yekoc/LegendAPI).

> **Slingshot**: Hold to pull back and charge, then release to launch yourself across the room!

- **Tap** dash: a normal dash.
- **Hold** dash: your wizard plants while dust builds at your feet (up to 1 second).
- **Release**: launch the way you're aiming. A full charge gives **+60% dash speed and +40% dash
  time** (about 2.2× the distance), and the launch spot bursts with wind that knocks nearby
  enemies away. Holding the full second launches you automatically.

You're not invulnerable while charging, so time it.

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLSlingshotDash.zip`
from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it
into your Wizard of Legend folder. Find it in the arcana shop, or install **Unlock Mod Content** to
have it unlocked right away.

## Troubleshooting

The mod finds the game's dash button by name. `BepInEx\LogOutput.log` has a `Dash button action:`
line showing which one it picked, or `No dash action found`, in which case Slingshot works as a
normal dash.
