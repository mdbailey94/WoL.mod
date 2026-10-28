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
  `worn_charm_<rarity>` (two frames side by side, the second the glint), all four rarities
  (the Common and Magic pendants arrived in 0.11.4). Uniques wear the Legendary sprites.
- The six unique powers use drawn effects: `fx_arc` (3 frames), `fx_burst` (4), `fx_motes` (2),
  `fx_gust` (2), `fx_echo` (3), `fx_thorns` (4, now one ring of thorns round the wizard instead
  of eight streaks). As with the icons, an `Art` folder beside the DLL overrides any of them.

## 0.11.5

- `[Debug] ShowcaseEnemy` with a name the game doesn't know no longer fails: F5 logs the
  game's enemy names (and the nearest match) and spawns Knights. The setting's description
  lists the common names.
- Legendary and unique charm stones float at 60% size (`[Look] LegendaryCharmScale`); four
  full 7x7 stones were a lot of orange round the wizard.

## 0.11.6

- `[Debug] ShowcaseHealth` above 0 makes F5 spawn three plain enemies (no affix, so no guard
  or evade) with that many times their health: targets that hold still for screenshots of
  burns and freezes. F5 works on a trial floor only, not in the house.
- A local build no longer copies the DLL into the base game's `BepInEx\plugins` (Chaos Depths
  belongs in its profile); pass `-p:ChaosDepthsProfileDir=<profile folder>` to copy it there.

## 0.11.7

- F12 (debug) dumps the game's loaded textures whose names match `[Debug] DumpTextures` as
  true-size PNGs into the mod's save folder under `dump\`, with a `sprites.txt` index of the
  sprites on each sheet, for reference art. `[Debug] ShowcaseEnemy` suggests GhoulLarge for
  status-effect screenshots.

## 0.11.8

- The profile installer's default folder is `%USERPROFILE%\ChaosDepths\profile`, outside
  `AppData`: a packaged app (the Claude desktop app, for one) redirects `AppData` writes into its
  own storage, which left the desktop shortcut launching the base game. It copies an older
  `%APPDATA%` profile across and warns when run from a sandbox. See `Profile/README.md`.

## 0.11.9

- The profile installer copies the base game's other mods (arcana, relics, menus, LegendAPI,
  patchers, and their configs) into the profile, so a dive plays with them; the base game keeps
  its own copies and still never loads Chaos Depths. `-OnlyChaosDepths` leaves them out.

## 0.11.10

- Panel: the selected item's description wraps over up to two lines and the comparison line
  over the room below, instead of one shortened line each (task sheet #6). The detail area is
  taller; the Totals block clips above it as before.
- Charm stones circle at the waist, `[Look] CharmOrbitHeight` 0.45 of the wizard's height
  (0.6, the chest, before; task sheet #3).
- Pendant art per view: `worn_pendant_<rarity>_down.png`, `_side.png` (drawn facing right,
  flipped for left) and `_up.png` (the chain at the back of the neck, behind the body) are used
  when present, else the one `worn_pendant_<rarity>.png` for every view (task sheet #2).

## 0.11.11

- The Magic pendant on the wizard is WoL Art's slim test amulet (task sheet #1): a thin V chain
  with a drop stone, front view 5x4 and side view 2x4 (`worn_pendant_magic_down/side.png`),
  for review in-game. The other rarities keep the 0.11.4 pendants until the set is approved.

## Inventory tabs and sorting (0.12.0, task sheet #7)

Laid out after WoL Art's mockup (`inventory_mockup_1x.png`):

- **Tabs** under the header: ALL, amulet, ring, charm, cycled with left / right (the chosen tab
  is lighter with a gold underline). In the Hub the depth picker moved to Y + left / right on a
  pad or Q / E on the keyboard, since left / right now change the tab.
- **Sort** (top right): Rarity, Level, Slot or Newest, cycled with Y (Interact) or Tab; equipped
  items always come first.
- **Rows** are two lines: the icon in a rarity-coloured frame, the name in its rarity colour
  with a green E when equipped, then the level and rarity. The chosen row is lighter with a gold
  bar on its left. As many rows as fit above the detail box.
- **Equipped** column: each slot an icon in its rarity frame with the item's name and level, or
  a faint silhouette and EMPTY. Totals below, when there's room.
- **Detail box**: name and type, the description over up to two lines, then VS EQUIPPED with
  each gain in green and each loss in red (and the unique power gained or lost).
- The panel's own frame, slot frames, dust icon and chevrons are still drawn in code until the
  pieces from task #16 arrive.

## 0.12.1

- The slim pendant set (task sheet #1, WoL Art): `worn_pendant_<rarity>_<down|side|up>.png`,
  all four rarities, the back view a chain at the nape drawn behind the body.
- The pendant follows the chest across the body in every frame, read from the picture's
  outline at chest height, so the run's forward lean no longer leaves it at the back of the
  neck; the height still comes from the standing anchor plus the run's bob.

## 0.12.2

- Inventory fixes from the first in-game check of 0.12.0: the tab row's chevrons were drawn
  several times too big; VS EQUIPPED showed a better cooldown in red (lower is now better for
  cooldowns as for damage taken); holding Y (or F) with left / right now picks the depth in the
  Hub, since a tap of Y changes the sort only on release.

## 0.12.3

- Hub depth picker: press Up from the top of the list to put the cursor on the depth line, then
  left / right; Down goes back to the list (Q / E still work anywhere in the Hub). The held
  Y + left / right of 0.12.0–0.12.2 is gone; Y (or F, or Tab) just changes the sort.
- VS EQUIPPED leaves out differences that round to 0%.
- Pendant: facing left it sits on the chest (the outline is now mirrored to match the picture
  as drawn), and its height follows each frame's own picture, so a lower run frame no longer
  leaves it on the hood. The ring on the hand uses the same mirror check.

## 0.13.0: WoL Art's new worn gear (task sheet #1, #23, #24)

- Each worn part picks its picture in this order: the unique's own design, then a Legendary's
  theme from its strongest stat (offense: Might, Precision, Ruin; defense: Vigor, Warding,
  Mending; utility: Haste, the Wind, Shadows), then its rarity.
- Pendants: the v3 slim set (about 125%), Cinderheart and Galeheart, and the three themed
  Legendaries (the defense one is the large gold pendant). Running, they swing back and forward
  once per stride (`_swing` frames); standing, Cinderheart's flames flicker (`_flicker`, 8 fps).
- Charms are one floating orb whose size goes with rarity (3, 4, 5, 7 px), shown at true pixel
  size; Bloodwell, Thornwick and the themed Legendaries have their own orbs, and every Legendary
  orb leaves drawn trail puffs. `LegendaryCharmScale` now only sizes the code-drawn stones.
- Rings: the new rarity bands, the Stormlinked Signet, the Twinfold Band and the themed ones.
- The ring and the pendant now find the hand and chest from the wizard's drawn pixels in every
  frame (the game's sheet is read once), not from the sprite's outline polygon, whose corners
  can lie past the picture: that put the ring out in front of the face in a side run (#24).

## 0.13.1 (task sheet #25)

- Picking an item up no longer also fires an arcana when Interact shares its button (X on the
  pad fired a basic attack). As with the game's relics, the press picks up only while standing,
  running or dashing, and from standing or running it is used up by the pickup.

## 0.13.2 (task sheet #26, frame-rate drops)

- `[Debug] HitchLog` (off by default): every frame slower than `HitchMs` (34 ms) is logged with
  whether the garbage collector ran in it and how long Chaos Depths' own Update and OnGUI took,
  and every 10 s a line gives the average and worst frame and the number of collections. Scene
  loads are left out. Turn it on, play where the drops happen, then read `BepInEx\LogOutput.log`.
- On-screen text (drop names, the rift timer, the inventory) no longer makes a new text style
  for every label several times a frame; each style is made once and kept.

## 0.13.3 (task sheet #17)

- Elites wear WoL Art's affix marker over their heads: a double chevron for Swift, a shield for
  Armored, a double up-chevron for Giant, pulsing at 7 fps at true pixel size. It sits above the
  game's own head point (where hats go), or the top of the picture when an enemy has none, keeps
  its size on a Giant and doesn't flip when the enemy turns. `[Elites] Markers` turns them off.

## 0.13.4 (task sheet #26)

- Less garbage every frame: the worn gear's pictures were looked up under a text key built
  (with numbers formatted) for every part on every frame; the key is now a plain value. The
  names over dropped items are made once per item instead of on every redraw.

## 0.13.5 (task sheet #18, #5)

- Dropped items sit on WoL Art's floor glow: a ring in the rarity's colour, with a light pillar
  on Rare and a tall pillar and sparkles on Legendary, pulsing at 6 fps (`drop_glow_<rarity>`;
  the code-drawn halo stays as the fallback).
- A worn unique gives the wizard its foot aura (`unique_aura_<name>`, 6 frames at 8 fps) behind
  the feet, from the first unique worn (amulet, then ring, then charm); it hides with the rest
  of the gear while riding or levitating. The glowing eyes come later (they need a face point
  found in every frame).

## 0.13.6 (task sheet #30)

- Two players: each opens their own inventory. The keyboard key belongs to the wizard playing on
  the keyboard and a pad's button to the wizard playing on that pad; before, a press went to the
  first wizard free to open it, so player 2 got player 1's inventory unless player 1 was busy.

## 0.13.7: a fresh save (task sheet #31)

- The profile now has two saves, picked on the title screen with **Tab** (keyboard) or **LB**
  (pad); the tag in the corner says which is on, and the choice is remembered (`[Profile] Save`,
  `Fresh` or `Debugging`).
  - **Fresh** (the default from now on): a new game save in `ChaosDepthsFresh` beside the game's
    save folder, with only the settings file (controls, screen, sound) carried over, and its own
    characters for player 1 and player 2 in `BepInEx\config\ChaosDepths\Fresh`. Every arcana,
    mod ones too, is bought in the shop as on any new save (Unlock Mod Content stands down in it).
  - **Debugging**: the save used up to now, untouched (`ChaosDepths` save folder, characters in
    `BepInEx\config\ChaosDepths`), for testing.
- Switching saves on the title screen writes out the characters of the save being left, points
  the game at the other save's folder and loads it the way the game does when it starts.

## 0.13.8: balanced drops in the fresh save (task sheet #31)

WoL Player's first-pass rates, for the fresh save only (the Debugging save keeps the old ones):

- An ordinary kill drops an item 6% of the time (`[Loot] FreshDropChance`; was 15%).
- Rarity weights Common 62 / Magic 28 / Rare 8.5 / Legendary 1.5 (the fresh save's own
  `loot.json`, written doubled as 124 / 56 / 17 / 3); Ascension's rarity boost stacks on top.
- 30% of Legendaries are uniques (`[Uniques] FreshUniqueChance`; was 50%).
- The first realm boss each character beats drops a guaranteed Legendary.
- Elites, floor bosses, realm bosses and rifts drop as before; each drop is the killer's own.

## 0.13.9 (task sheet #33)

- `[Debug] PortalLog` (on): each Interact press outside the trials logs every portal or exit pad
  within 5 units and what the game checks before it loads (whether it has caught the wizard,
  the distance, whether the wizard is free), to find why the Hub's trials portal ignored it.

## 0.14.0: no more depths; difficulty from Ascension (task sheet #27)

- **Depths are gone**: no depth scaling, no waypoints, no depth line or picker in the Hub's
  inventory panel, and so no layer bosses (they came every 5 depths). Rifts stay.
- **Ascension's approved 1–10 table** (Ascension 0.7.0 holds it; see its README) drives the rest:
  - elite chance 12% at level 1 up to 40% at 10 (level 0 keeps `[Elites] Chance`);
  - elites with **two affixes** (1 in 4 from level 4, half from 8, all at 10): both effects, a
    tint between their colours, and both markers side by side;
  - from level 5 bosses get an elite affix (Swift or Armored) and elites drop Rare or better;
    from 9 every elite brings a **partner** (another of the same enemy, same affixes, summoned
    beside it); at 10 realm bosses get two affixes and drop a Legendary;
  - from level 7 a floor opens as a rift 40% of the time;
  - rewards: item level +2 a level (was depth's +1), experience +5% a level; Magic-and-better
    weight +15% a level as before.

## 0.14.1 (task sheet #32)

- A completed dive (its final boss, or its last realm boss with `[Dive] RealmsPerDive`) is
  reported to Ascension 0.7.1, which unlocks the next level in the fresh save.

## 0.14.2 (task sheet #5)

- A worn unique's **glowing eyes** (WoL Art's `unique_eyes_<name>`, 4 frames at 6 fps) sit on
  the face in every frame: on the row 7 pixels below the top of the wizard's drawn picture, in
  its middle facing down, one eye two pixels in from the front edge facing sideways, hidden
  facing up. Placed on the wizard's own pixel grid; with the foot aura from 0.13.5.

## 0.14.3: the inventory in WoL Art's pieces (task sheet #16)

- The inventory panel is drawn with WoL Art's pieces on the layout of her mockup
  (`inventory_mockup_1x.png`, 232 x 168): 9-slice panel and insets, tabs, chevrons, rarity slot
  frames, the selection cursor, empty-slot silhouettes and the Chaos Dust icon. Every position
  is in the mockup's pixels times a whole number, so the pieces stay crisp (x3 in a 1280x720
  window, x5 at 1080p). Added to the mockup: the character's level beside the title, its
  experience along the title's rule, and the list's item count in the tab row.
- `[Panel] Look = Coded` brings back the code-drawn panel.

## 0.14.4

- A pack of elites no longer piles its markers into one cluster: where markers of different
  elites overlap, only the one over the elite nearest a wizard shows (an elite's own two
  markers always show side by side).
- The log no longer says "at depth N" for rifts.

## 0.14.5

- The inventory's text a size larger, nearer the mockup's (from WoL Player's first screenshot).
- `[Debug] PortalLog` only logs presses within 3 units of a portal (the Ascension altar beside
  the trials portal filled the log).

## 0.14.6 (task sheet #26)

- `[Display] FrameCap` (0 = off): caps the frame rate. The game never caps it itself, so with
  its vsync option off it runs at 500-1400 fps, and the frame probe showed a ~100 ms stall every
  few seconds even on the title screen with no mod code behind it. Try 60 or 120 (or the game's
  vsync) if the game stutters.

## 0.14.7 (task sheet #16)

- VS EQUIPPED shows the losses again: gains first, then losses, carried onto a second line when
  the first is full (on one line the losses at its end were cut off).
- The tab row shows where the list is when it scrolls ("5-8 OF 12").

## 0.14.8

- The frame cap from 0.14.6 moved to Mod Menu 0.1.5 (`[Display] FrameCap`, 120 by default), so
  it also works in the base game; at 120 the stalls WoL Player measured were gone.

## 0.14.9 (task sheet #30)

- Two players: an open inventory now reads only its owner's controls. The keyboard drives only
  the keyboard wizard's inventory, and a pad wizard's inventory reads only that wizard's own
  controller. Before, the panel read the whole keyboard whoever had it open, so player 1 walking
  or pressing Space, Delete or Escape moved through, equipped, salvaged in or closed player 2's
  inventory; and a pad that Rewired lists under both players could move player 1's. With one
  wizard nothing changes: keyboard and pad both work.
