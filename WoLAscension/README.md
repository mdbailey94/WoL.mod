# Ascension (Wizard of Legend)

Ascension levels 1–10 for harder runs. Pick your level as you enter the Chaos Trials; each level adds a modifier on
top of the ones before it.

The table since 0.7.0 (WoL Player's, approved by Matt; task sheet #27/#28). "Damage" is the
damage you take. The elite columns and the loot rewards are applied by Chaos Depths, which reads
this table; without Chaos Depths only the health, damage, speed and healing parts apply.

| Level | Elites | Enemy health | Damage | New at this level |
|-------|--------|--------------|--------|-------------------|
| 1 | 12% | +20% | +10% | (entry level) |
| 2 | 15% | +30% | +20% | your healing −25% |
| 3 | 18% | +40% | +30% | enemies move 10% faster |
| 4 | 22% | +55% | +40% | 1 in 4 elites has two affixes |
| 5 | 25% | +70% | +50% | bosses +30% health and an elite affix; elites drop Rare or better |
| 6 | 28% | +85% | +60% | your max health −15% |
| 7 | 31% | +100% | +75% | enemies 20% faster (replaces 3); rifts 40% of floors |
| 8 | 34% | +120% | +90% | healing −50% (replaces 2); half of elites have two affixes |
| 9 | 37% | +140% | +105% | elites come in pairs; bosses +60% (replaces 5's +30%) |
| 10 | 40% | +160% | +120% | every elite and realm boss has two affixes; the realm boss drops a Legendary |

**The rewards**, per level: +5% chaos gems and +5% experience, Magic-and-better rarity weight
+15%, item level +2. Gold drops are no longer cut (0.6.x had −20% from level 5).

Not in yet: level 10's Chaos Surge look (waiting on art, #29). (Attack speed is in since 0.7.3.) Bosses with an affix roll Swift or Armored (a Giant boss
would be scaled up inside its arena).

## Choosing a level: the Ascension altar

In the plaza, an **Ascension altar** stands to the left of the Chaos Trials portal: a carved stone
totem with a gold-rimmed brazier on top, softly shaded like the game's own props, that you can't
walk through. A **flame** burns in the brazier, growing from a small cold blue one to a big
white-hot one as the level rises (a grey ember when off); ten **rune notches** up the column light
one by one with the level, the **level number** glows on the plinth, and the flame throws a glow, a
ring of light on the ground, drifting embers and a point light, all in its colour. Walk up to it and
the game's own button prompt appears; **interact** to raise the level by one (after 10 it goes back
to OFF): the flame flares, sparks and dust fly, and the game's on-screen notice shows the level,
what it adds and the gem bonus. Raising the level never fires the attack or dash that shares
Interact's button (gamepads put Interact on a face button): while you hold Interact at the altar,
the skill buttons read as released. The altar keeps clear of the plaza's NPCs (if the spot beside
the portal is taken, it moves a little down or sideways and says so in the log).

Then just take the portal: the run starts at the altar's level, with no pause or menu, and the
notice shows it as you go in. The level is locked for the run, a small "ASCENSION n" tag shows in
the corner (`ShowLevelInRun`), and all modifiers come off when you leave the trials (dying,
quitting or finishing). The modifiers use the game's own stat system, so they stack normally with
relics. The altar remembers your level between runs and sessions. You can also turn the whole mod
off in the title screen Mods menu.

The altar finds the portal by itself; if it can't, it uses where your last run's portal was (so
after one run it's there for good). The log says where it put it (`Ascension altar placed in ...`)
or lists the level loaders it saw. If the altar is in an awkward spot, move it with
`[Altar] LeftOfPortalX` / `LeftOfPortalY` in
`BepInEx/config/mdbailey94.wol.ascension.cfg` (game units from the portal; default 3.5 to the left,
level with it). If a run starts some way that skips the portal, the altar's level is applied as soon as
the first enemies appear.

## Changes in 0.8.0: the whole level table is in the config (asked for by Matt)

- Every value of every level is a setting: one section per level, `[Level 01]` to `[Level 10]`
  in `mdbailey94.wol.ascension.cfg`, each holding that level's whole value (not what it adds
  to the level below: `[Level 05] EnemyHealth = 1.7` means +70%). The defaults are the table
  as it was, so nothing changes until you edit them. Change them with the game closed, or
  they apply on the next launch.
- Per level: `EnemyHealth`, `EnemyDamage` (what the wizard takes), `EliteChance`, `Healing`,
  `EnemySpeed`, `Gold`, `BossHealth` (on top of `EnemyHealth`), `PlayerMaxHealth`, `ChaosGems`,
  `Experience`, `TwoAffixEliteChance`, `BossAffixes` (floor bosses), `RealmBossAffixes`,
  `ElitesInPairs`, `RiftChance`, `ItemLevelBonus`, `ElitesDropRare` and
  `RealmBossDropsLegendary`. The elite, boss-affix, rift, experience and loot ones are applied by Chaos Depths.
- The altar's "Adds:" line is now worked out from the settings, so it stays right when you
  change them (the wording is a little different: "25% of elites have two affixes").

## Changes in 0.7.5

- Each of Chaos Depths' save slots (0.15.0) keeps its own unlocked Ascension level, starting at
  0: Save 1 in `[Unlocks] FreshSaveHighest`, Save 2 in `Save2Highest`, and so on. The Debugging
  save and the base game keep all ten (`OtherSaves`).

## Changes in 0.7.4

- Chaos Depths' fresh save starts at **Ascension 0** (off) with nothing unlocked: beat a run at 0
  to unlock 1, beat 1 to unlock 2, and so on up to 10 (asked for by Matt). The setting is now
  `[Unlocks] FreshSaveHighest` (0 to start); the old `FreshSave` key, which started at 1, is no
  longer read.

## Changes in 0.7.3

- Enemies now also **attack** faster at levels 3 (+10%) and 7 (+20%), as the table says: their
  animations, which pace their wind-ups and swings, are sped up the way the game's own
  attack-speed relic speeds up the wizard.

## Changes in 0.7.2

- The level 1 notice no longer lists the health and damage twice (and shows its percent signs).

## Changes in 0.7.1

- Levels unlock in order (task sheet #32). In Chaos Depths' **fresh save** the altar starts with
  only level 1; beating a run at your highest level unlocks the next one (a banner says so).
  "Beating" is a Chaos Depths dive reported complete, or, without Chaos Depths, leaving the final
  boss's arena for the ending. Every other save (the base game, Chaos Depths' Debugging save)
  keeps all ten. `[Unlocks] FreshSave` / `OtherSaves` hold the highest level for each.

## Changes in 0.7.0

- The approved 1–10 table above in place of the old one (task sheet #27/#28).

## Changes in 0.6.1

- The level panel's text style is made once and kept instead of anew several times a frame (task
  sheet #26, frame-rate drops).

## Install

Download `WoLAscension.zip` from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest)
and unzip it into your Wizard of Legend folder. Only BepInEx is needed.

## Settings

`BepInEx\config\mdbailey94.wol.ascension.cfg`: `Enabled`, `Level` (0–10, the altar's current level),
`ShowLevelInRun`; under `[Altar]`, `LeftOfPortalX` / `LeftOfPortalY` (where it stands) and `Effects`
(the glow, ground ring, embers and light; off leaves just the totem, its flame and runes).

## Troubleshooting

`BepInEx\LogOutput.log` records each portal ("Portal to '...' (starts a run: true)"),
"Starting run at Ascension n", "Left the trials", and every level load.
