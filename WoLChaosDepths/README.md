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

5. **Shaping a dive** (0.3.0). Three `[Dive]` settings, each defaulting to the game's own
   behaviour: `Realm` (which realm the run starts in: Earth, Fire, Water, Air, Lightning, or
   `Game` for the game's order), `FloorsPerRealm` (floors before each realm's boss; 0 = the
   game's usual two), and `RealmsPerDive` (realm bosses to beat before the dive ends and you return
   to the Hub; 0 = the whole run). Every level load logs what the game thinks the run is: stage,
   tier, floors per realm, the realm order and the boss scenes.

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
- Debug keys (with `[Debug] Enabled`): F6 kill the room, F7 clear the floor (skip to the exit
  room, skull altar, kill the mini-boss, exit pad, Continue), F8 god mode (a toggle: press once),
  F9 gold, F10 auto-dive.
- Dive test, hands-off: set `[Debug] Enabled = true`, `Realm = Fire`, `FloorsPerRealm = 1`
  (the game's own is 2, so 1 shows the setting working and is quicker), `RealmsPerDive = 1`; walk into the trials portal; on the first floor press **F10**. The
  auto-dive turns god mode on, kills the room, skips to the next floor, and repeats until the run
  leaves the trials, logging `Auto-dive step N` lines. Other keys: F6 kill all, F7 skip floor,
  F8 god mode, F9 gold. Expected: the first floor is a Fire floor (log `Dive: first realm X -> Fire`), the boss
  comes after 1 floor (each floor: skip to the exit room, step on the skull altar, kill the
  mini-boss, take the exit portal that appears), and leaving the boss arena shows `Dive complete!` and lands you in the
  Hub after 2 floors. The `Dive: '...' -> '...'` log lines show the counters at each load; send them if any step
  doesn't match.

## Settings

`BepInEx\config\mdbailey94.wol.chaosdepths.cfg`: `HealthPerLevel`, `XpPerEnemyHealth`,
`BossXpMultiplier`, `LevelCap`, `DropChance`, `BossDropMinRarity`, `AutoEquip`, `LogKills`,
and under `[Panel]` the open controls: `KeyboardKey` (I) and `ControllerButton` (RightStick,
LeftStick, Back or None).

To start over, delete the `characterN.json` files while the game is closed.

## Elites and Ascension (0.4.0, milestone M3)

- In the trials, each ordinary enemy has an 8% chance (`[Elites] Chance`) to spawn as an
  elite: 2.5x health, a tint, and one affix. **Swift** moves 40% faster (blue), **Armored** takes
  40% less damage (grey), **Giant** is drawn bigger with 1.5x more health again (red).
- Elites give extra experience and always drop an item of at least Magic rarity.
- The floor boss summoned at the skull altar now has its own rewards (`FloorBossXpMultiplier`
  4, `FloorBossDropMinRarity` Magic) instead of a realm boss's (10x experience, Rare or better).
- With the Ascension mod installed, each Ascension level adds 1% elite chance, 15% weight to
  Magic and better rarities (`[Loot] RarityBoostPerAscension`), and one item level.
- Log lines: `Elites: chance ...` once per floor, `Elite: Swift Archer (health ...)`, and
  `... (Swift elite) slain by player 1`.
