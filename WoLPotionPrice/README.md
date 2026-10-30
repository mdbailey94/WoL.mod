# Potion Price (Wizard of Legend)

Health potions cost more each time you buy one in a run, as they used to. (In the current game
the price increase is set to zero, and each potion only heals less than the last.)

Each potion costs **25% of its base price more for every potion already bought this run**
(`IncreasePercent`), counted by the game itself, so it starts again with each run. Potions are
priced as they appear in a shop and again when one is bought, and the price tag redraws each time
(0.1.0 changed the price but never told the tag, so it kept showing the base price).

## Settings

`BepInEx\config\mdbailey94.wol.potionprice.cfg`:

- `Enabled`: on or off.
- `IncreasePercent`: how much more each potion costs per potion already bought, as a percent of
  its base price (default 25).

The log shows `Potion Price 0.2.0 loaded (+25% a potion)` at startup and
`Potions bought this run: N; potion price X -> Y` whenever the price changes.

## Install

Needs BepInEx only. Download `WoLPotionPrice.zip` from the
[latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it into
your Wizard of Legend folder.
