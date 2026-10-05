# Extended Stats (Wizard of Legend)

An in-game panel that shows the numbers the game keeps hidden. Press **F2** to show or hide it.

**Wizard**: health, shield, armor, evade, damage taken, healing, move and dash speed, gold
and platinum.

**Arcana**: for each equipped skill, its damage, crit chance, crit damage and cooldown.
These include bonuses from relics and outfits. Empowered skills are marked *(enhanced)*.

**This run**: damage dealt (basic attacks and arcana), damage taken, enemies defeated and
gold spent. These come from the game's own end-of-run tracking.

The panel updates four times a second and scales with your screen resolution.

## Install

Download `WoLExtendedStats.zip` from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest)
and unzip it into your Wizard of Legend folder. Only BepInEx is needed, not LegendAPI.

## Settings

In `BepInEx\config\mdbailey94.wol.extendedstats.cfg`:

| Setting | Default | |
|---------|---------|-|
| `Toggle` | F2 | Show/hide key |
| `Visible` | true | Whether the panel starts visible (remembers your last toggle) |
| `Scale` | 1.0 | Panel size, 0.5 to 2 |

## Building it yourself

Same as [Rolling Gale](../WoLRollingGale/README.md#building-it-yourself) (no `LegendApi.dll` needed).
