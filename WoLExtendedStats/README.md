# Extended Stats (Wizard of Legend)

An in-game panel that shows the numbers the game keeps hidden.

- **Controller or keyboard:** open your character/equip menu (**Select** on a controller) and your
  stats panel appears next to it. It closes when the menu does.
- **Pin it:** press **F2** to keep the panels on screen all the time, and press F2 again to unpin.
- **Co-op:** each player gets their own panel. Player 1's is on the left and player 2's on the right.

**Wizard**: health, shield, armor, evade, damage taken, healing, move and dash speed, gold
and platinum.

**Arcana**: for each equipped skill, its damage, crit chance, crit damage and cooldown.
These include bonuses from relics and outfits. Empowered skills are marked *(enhanced)*.

**This run**: damage dealt (basic attacks and arcana), damage taken, enemies defeated and
gold spent. These come from the game's own end-of-run tracking.

The panels update four times a second and scale with your screen resolution.

## Install

Download `WoLExtendedStats.zip` from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest)
and unzip it into your Wizard of Legend folder. Only BepInEx is needed, not LegendAPI.

## Settings

In `BepInEx\config\mdbailey94.wol.extendedstats.cfg`:

| Setting | Default | |
|---------|---------|-|
| `Toggle` | F2 | Pin/unpin key |
| `Visible` | false | Panels pinned on screen (remembers your last F2 toggle) |
| `ShowWithCharacterMenu` | true | Show a player's panel while their character menu is open |
| `Scale` | 1.0 | Panel size, 0.5 to 2 |

## Building it yourself

Same as [Rolling Gale](../WoLRollingGale/README.md#building-it-yourself) (no `LegendApi.dll` needed).
