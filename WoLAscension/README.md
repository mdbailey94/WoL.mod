# Ascension (Wizard of Legend)

Ascension levels 1–10 for harder runs. Pick your level in the hub; each level adds a modifier on
top of the ones before it.

| Level | Adds |
|-------|------|
| 1 | Enemies +15% health |
| 2 | Enemies deal +15% damage |
| 3 | Your healing −25% |
| 4 | Enemies move 10% faster |
| 5 | Gold drops −20% |
| 6 | Bosses +25% health |
| 7 | Your max health −15% |
| 8 | Enemies +30% health (replaces level 1) |
| 9 | Enemies deal +30% damage (replaces level 2) |
| 10 | Healing −50% and enemies 20% faster (replace levels 3 and 4) |

## Choosing a level

In the hub (your house), a panel at the top shows the level and what it does. Change it with:
- **Controller:** open the character menu (**Select**) and push the **right stick left/right**
- **Keyboard:** **[** and **]**
- **Mouse:** click the **<** and **>** arrows

The level is locked once you leave the hub, and a small "ASCENSION n" tag shows in the corner
during the run. Back in the hub, all modifiers come off. The modifiers use the game's own stat
system, so they stack normally with relics.

Level 0 means normal difficulty. You can also turn the whole mod off in the title screen Mods menu.

## Install

Download `WoLAscension.zip` from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest)
and unzip it into your Wizard of Legend folder. Only BepInEx is needed.

## Settings

`BepInEx\config\mdbailey94.wol.ascension.cfg`: `Enabled`, `Level` (0–10), `ShowLevelInRun`.

## Troubleshooting

`BepInEx\LogOutput.log` records "Entered the hub" and "Left the hub - running at Ascension n",
plus every level load, so you can see when it switches on and off.
