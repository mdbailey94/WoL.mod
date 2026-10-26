# Chaos Depths (Wizard of Legend) - EXPERIMENTAL, M0 technical tests

The technical tests for the Chaos Depths RPG plan: a **persistent character** on top of a normal
run, and one custom screen. It proves four things the plan depends on:

1. **A save of its own.** Each player gets `BepInEx\config\ChaosDepths\characterN.json` (with a
   `.bak` of the previous save). The game's own save is never touched.
2. **Experience and levels.** Kills in the trials give experience (about 1 per 8 points of the
   enemy's max health; bosses x10). Levels need 60 x level^1.4 experience each (60, 158, 280...),
   give +2% max health apiece and show a `Level N!` notice. Cap 50.
3. **Random items from a data file.** 15% of kills (and every boss) drop an item rolled from
   `BepInEx\config\ChaosDepths\loot.json`: a rarity (Common 55% / Magic 30% / Rare 12% /
   Legendary 3%), an Amulet, Ring or Charm, and 1 to 4 affixes (damage, critical chance and
   damage, cooldowns, health, move speed, healing, damage taken, evasion) scaled by your level.
   Found items go into the save; until there is an inventory screen, an item is **equipped
   automatically when it beats the one in its slot**, and its stats go on through the game's own
   stat modifiers.

4. **A custom screen on a controller.** Press **R3** (click the right stick) or **I** on the
   keyboard to open the inventory over the paused game: your level and experience bar, the three
   equipped slots and the inventory list with rarity colours. Move with the stick or arrow keys;
   **A / Enter** equips or unequips the highlighted item (an asterisk marks equipped ones);
   **hold A** for 0.7 s, or press **Delete**, to salvage it into Chaos Dust (1 / 3 / 8 / 20 by
   rarity); **B / Esc** or the open key closes. The game's own pause menu is left alone.

`loot.json` is written with the defaults the first time and can be edited: weights, affix ranges,
labels and suffixes.

Both files are read and written by the mod's own small JSON code (`Json.cs`): 0.1.0 used Unity's
`JsonUtility`, which in this game's Unity (5.6) can't see a plugin's classes, so it wrote `{}` and
nothing was kept. 0.1.1 rewrites a `{}` `loot.json` with the defaults; an empty `characterN.json`
from 0.1.0 starts a fresh character.

## What to test

- Play a few floors. The log should show `Player 1: Wizard, level 1, 0/60 xp`, then `found:` lines
  and `reached level N`. A `Level 2!` notice should appear after about 60 experience.
- Quit and restart: the log's `Loaded ... character1.json` line should show the level and item
  count you had.
- `Stats applied ... items: Damage +12%` lines show the equipped bonuses; check a damage number
  on a dummy before and after a damage item.
- In co-op, each player has their own file and level.
- Open the inventory with R3 (or I), move, equip something, salvage something, close: the game
  should pause while it's open and carry on after. The log shows `Inventory opened`, `Equipped`,
  `Salvaged` lines.

## Settings

`BepInEx\config\mdbailey94.wol.chaosdepths.cfg`: `HealthPerLevel`, `XpPerEnemyHealth`,
`BossXpMultiplier`, `LevelCap`, `DropChance`, `BossDropMinRarity`, `AutoEquip`, `LogKills`,
and under `[Panel]` the open controls: `KeyboardKey` (I) and `ControllerButton` (RightStick,
LeftStick, Back or None).

To start over, delete the `characterN.json` files while the game is closed.
