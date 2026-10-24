# Potion Price (Wizard of Legend)

Health potions cost more each time you buy one in a run, as they used to. (In the current game
the price increase is set to zero, and each potion only heals less than the last.)

Each potion costs **25% of its base price more for every potion already bought this run**
(`IncreasePercent`), counted by the game itself, so it starts again with each run.

## Settings

`BepInEx\config\mdbailey94.wol.potionprice.cfg`:

- `Enabled`: on or off.
- `IncreasePercent`: how much more each potion costs per potion already bought, as a percent of
  its base price (default 25).

The log shows `Potions bought this run: N; price X -> Y` when the price changes.

## Install

Needs BepInEx only. Download `WoLPotionPrice.zip` from the
[latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it into
your Wizard of Legend folder.
