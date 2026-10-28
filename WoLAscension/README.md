# Ascension (Wizard of Legend)

Ascension levels 1–10 for harder runs. Pick your level as you enter the Chaos Trials; each level adds a modifier on
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

**The reward:** every level also gives **+4% chaos gems** during the run (+40% at level 10). Gems
come in small amounts, so the bonus builds up fractions between pickups instead of rounding them
away.

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
