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
- F5 (with `[Debug] Enabled`) spawns a Swift, an Armored and a Giant elite beside the wizard
  (`[Debug] ShowcaseEnemy`, default Knight) to check the tints, size and speed.
- Log lines: `Elites: chance ...` once per floor, `Elite: Swift Archer (health ...)`, and
  `... (Swift elite) slain by player 1`.

## Unique legendaries (0.5.0, milestone M3)

Half of all Legendary drops (`[Uniques] UniqueChance`) are uniques: a named item with its usual
affixes plus a power that works while it is equipped.

| Unique | Slot | Power |
|---|---|---|
| Stormlinked Signet | Ring | Each hit arcs 35% of its damage to the nearest enemy within 6 units (a cyan line) |
| Cinderheart | Amulet | Enemies you kill burst for 40% of their max health on enemies within 3.5 units (an orange ring) |
| Bloodwell Charm | Charm | Each kill restores 2% of your max health |

Powers don't trigger each other, and their damage counts as yours (experience and drops).
The panel shows the power first in the item's description. With `[Debug] Enabled`, F4 puts one
of each unique in your inventory.

## Depth and waypoints (0.6.0, milestone M4)

Every dive has a depth. It starts at the depth chosen in the Hub and goes one deeper with
each floor and boss arena. Below depth 1, each depth adds (all under `[Depth]`):

| Setting | Default | Effect per depth |
|---|---|---|
| EnemyHealthPerDepth | 0.06 | +6% health for enemies and bosses |
| EnemyDamagePerDepth | 0.04 | +4% damage you take |
| XpPerDepth | 0.05 | +5% experience |
| RarityBoostPerDepth | 0.05 | +5% weight for Magic and better |
| ItemLevelsPerDepth | 1 | +1 item level |

Each character remembers the deepest depth it has reached: its waypoint (a notice shows
"Waypoint: depth N"). In the Hub, open the inventory panel and press left or right (or the
stick) to pick where the next dive starts, from 1 up to the waypoint. In a run the panel shows
the current depth. Log lines start with `Depth:`.

## Layer bosses and rifts (0.7.0, milestone M4)

- **Layer bosses.** Every 5 depths (`[Layers] LayerEvery`), the realm boss met at that depth is
  a layer boss: +50% health on top of depth, a pink tint, double experience, and a Legendary
  drop for each player who hurt it. Notice: "Layer N boss: <name>".
- **Rifts.** A trial floor (never a boss arena) has a 1 in 4 chance (`[Rifts] Chance`) to open
  as a rift for 3 minutes (`Seconds`): +25 points of elite chance and +50% Magic-and-better
  weight while it is open, with a timer at the top of the screen. Leave the floor through its
  exit before the rift collapses and each player gets a bonus item of Rare or better
  (`RewardMinRarity`). With `[Debug] Enabled`, F3 opens a rift on the current floor.
- Log lines: `Layer boss at depth N: ...`, `Rift opened at depth N: ...`, `Rift at depth N:
  cleared with S s to spare` or `collapsed`.

## Physical drops (0.8.0, milestone M5)

Items now land on the floor where the enemy died: a small gem in the rarity's colour (grey,
blue, gold, orange; a star for a unique), bobbing over a glow, bigger for Rare and Legendary, with a white outline and a pulsing glow for Legendary.
Its name shows above it when a wizard is within 3.5 units (`[Loot] NameRadius`); within 1.3
(`PickupRadius`) the game's button prompt appears over it and **Interact** picks it up, like a
relic (`PressToPickUp = false` goes back to picking up by walking over). Any item still on the floor when you leave it is collected
for its owner (`CollectLeftovers`), so nothing is lost. `PhysicalDrops = false` puts drops
straight into the inventory as before. Rift rewards and F4's uniques go straight in. Log lines:
`Player 1's drop: ... , on the floor`, then `Player 1 found: ...` on pickup or `collected: ...`
for leftovers.

## Equipped items on the wizard (0.9.0, milestone M5)

What you wear shows on the wizard, drawn in the game's pixel style: the amulet as a pendant on
a short chain at the chest; the ring as a band on the leading hand, found on each frame of the
animation from the picture's outline so it stays on the hand while running; the charm as stones
floating in a circle round the wizard at chest height, bobbing as they go, one stone for Common
up to four for Legendary (Rare and up leave a fading pixel trail). Everything grows and glints
more with rarity. Each is in
its item's rarity colour; a unique is in its power's colour (storm cyan, ember orange, blood
red) and sheds a single-pixel mote now and then. They are placed on the wizard's picture as drawn each frame, so they move with the
animation, follow the facing, and sit behind the body when facing up. `[Look] ShowEquipped` and `UniqueSparks` switch them off.

## Items with their own artwork, more uniques, a fuller panel (0.10.0, milestone M5)

- **Artwork.** Every item has a 14x14 picture of its type (ItemArt.cs): an amulet on its chain, a
  ring seen from above with its stone, a charm pouch on a cord. The picture gains with rarity:
  Common is dull iron and a grey stone; Magic colours the stone and lights a facet; Rare makes
  the metal gold and adds a spark; Legendary adds a gold setting and a halo. Each unique has a
  picture of its own (a signet ring with a lightning face, an ember heart, a vial of blood, a
  swirl stone, two interlocked rings, a thorn-wound token). The same picture is the drop on the
  floor and the icon in the panel.
- **Uniques.** Two per slot now. New: **Twinfold Band** (Ring, every 4th hit on an enemy strikes
  it again for 60%), **Galeheart** (Amulet, kills give +30% move speed for 3 s), **Thornwick
  Charm** (Charm, being hit lashes enemies within 2.5 for 50% of the damage taken, once a
  second). All six powers are drawn in pixel style (PowerFx.cs): a jagged bolt for the arc, a
  ring breaking into dashes for the burst, red motes flying to the wizard for the leech, white
  streaks for the gale, a violet flash for the echo, thorns snapping out for the lash. All under
  `[Uniques]`.
- **Panel.** Item icons in the list, the equipped slots and the detail; a **Totals** block under
  the equipped slots adding up every bonus you wear; a CHAOS DEPTHS title.
- **Profile.** `Profile/Install-ChaosDepthsProfile.ps1` sets Chaos Depths up as its own game
  profile with a desktop shortcut, leaving the base game and its arcana and relic mods alone
  (see `Profile/README.md`). The log says `loaded (profile)` and the Hub shows a CHAOS DEPTHS
  tag when launched that way.

## Its own game save (0.11.0)

- In the profile the game's own save (unlocks, loadout, gold, gems, settings) lives in a
  `ChaosDepths` folder beside the game's save folder. The game fills its save root from a private
  field only when that field is empty, so the mod fills it first; no save code is patched.
- The first time the profile loads, the base save is copied in as its starting point. After that
  the two saves are independent, so a profile session can no longer rewrite the base game's
  loadout. `[Profile] SeparateSave` and `SeedFromBaseSave` control it. The log says
  `Profile save: ...` with the folder in use.

## Drawn item artwork (0.11.1)

- The twelve item icons (amulet, ring, charm at Common, Magic, Rare, Legendary) are now pixel
  art drawn for the mod, 16x16, in `Art/` and embedded in the DLL. A PNG of the same name in an
  `Art` folder beside the DLL overrides it at runtime, so a new piece can be previewed by
  copying it in; a missing picture falls back to the code-drawn one. Uniques will take
  `unique_<key>.png` the same way; the six unique pictures arrived in 0.11.2.

## Drawn worn gear and power effects (0.11.3)

- The gear on the wizard uses drawn sprites: `worn_pendant_<rarity>`, `worn_ring_<rarity>`,
  `worn_charm_<rarity>` (two frames side by side, the second the glint). Common and Magic
  pendants are still code-drawn until their art arrives. Uniques wear the Legendary sprites.
- The six unique powers use drawn effects: `fx_arc` (3 frames), `fx_burst` (4), `fx_motes` (2),
  `fx_gust` (2), `fx_echo` (3), `fx_thorns` (4, now one ring of thorns round the wizard instead
  of eight streaks). As with the icons, an `Art` folder beside the DLL overrides any of them.
