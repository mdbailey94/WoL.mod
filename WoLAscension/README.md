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

Not in yet: the enemies' attack speed (only their movement is faster at 3 and 7) and level 10's
Chaos Surge look (waiting on art, #29). Bosses with an affix roll Swift or Armored (a Giant boss
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
