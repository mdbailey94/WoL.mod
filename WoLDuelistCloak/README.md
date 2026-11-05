# Duelist's Cloak (Wizard of Legend)

A new **cloak**, added with [LegendAPI](https://github.com/yekoc/LegendAPI): trade defence for
speed and crits.

| Stat | Value |
|------|-------|
| Movement speed | **+15%** |
| Critical chance | **+10%** |
| Critical damage | **+25%** (0.1.1 trimmed it from +30%: no bonus above a vanilla cloak's) |
| Damage taken | **+25%** (every hit on you is multiplied by 1.25) |

It's Awe's crits with Pace's speed, paid for by taking a quarter more damage from everything. Speed and crits are the game's own cloak stats; the damage taken is a custom
stat, because a negative Armor stat would do nothing on its own (the game never lets armour go
below zero).

The wardrobe and the merchant describe it the way they do vanilla cloaks (`- Increases ...` lines,
the last one `- Increased Damage Taken`); the pause menu adds the numbers (`( + 25 % )`).
It has its own colours (palette 13, which no vanilla cloak uses).

## Getting it

Buy it from the **cloak merchant in the Plaza**, for chaos gems like the other cloaks (same price:
cloaks don't have their own prices). While it's locked, the merchant always has it in one of
their three spots, usually the right-hand one (`AlwaysOnSale`); on its own, LegendAPI would only
offer it once fewer than three vanilla cloaks are left to buy. Then put it on at a wardrobe.

## With Cloak Upgrades

Upgrading raises its bonuses like any cloak's (speed up to +22.5%, crit chance up to +20%, crit
damage up to +50% at tier 4). The damage taken is a drawback and **never gets worse**: it stays
at +25% at every tier (and the in-run tailor doesn't raise it either).

## Checking it works

`BepInEx\LogOutput.log` shows:

- `Duelist's Cloak 0.1.1 registered (ID DuelistCloak): ...` at startup.
- `Duelist's Cloak is on sale at the cloak merchant` (or `... (in place of <cloak>)`) in the Plaza.
- `Duelist's Cloak equipped (player 1): damage taken +25% on, damage taken multiplier now x1.25`
  when you put it on (and each time a level loads), `... removed ...` when you take it off.

## Settings

`BepInEx\config\mdbailey94.wol.duelistcloak.cfg`:

- `[General] Enabled`: offer the cloak at the merchant (also in the title screen **Mods** menu).
  Turning it off doesn't take it away once bought.
- `[General] AlwaysOnSale`: keep a merchant spot for it while it's locked.

The stats are the constants at the top of `DuelistCloakPlugin.cs`.

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLDuelistCloak.zip`
from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it
into your Wizard of Legend folder.
