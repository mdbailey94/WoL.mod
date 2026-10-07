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

In the plaza, an **Ascension altar** stands beside the Chaos Trials portal: a stone pedestal with
a floating chaos crystal. Walk up to it and the game's own button prompt appears; **interact** to
raise the level by one (after 10 it goes back to OFF). The crystal burns brighter the higher the
level, and the game's notice banner shows the level, what it adds and the gem bonus.

Then just take the portal: the run starts at the altar's level, with no pause or menu, and the
banner shows it as you go in. The level is locked for the run, a small "ASCENSION n" tag shows in
the corner (`ShowLevelInRun`), and all modifiers come off when you leave the trials (dying,
quitting or finishing). The modifiers use the game's own stat system, so they stack normally with
relics. The altar remembers your level between runs and sessions. You can also turn the whole mod
off in the title screen Mods menu.

If the altar is in an awkward spot, move it with `[Altar] OffsetX` / `OffsetY` in
`BepInEx/config/mdbailey94.wol.ascension.cfg` (game units from the portal; default 3.5 to the
left). If a run starts some way that skips the portal, the altar's level is applied as soon as
the first enemies appear.

## Install

Download `WoLAscension.zip` from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest)
and unzip it into your Wizard of Legend folder. Only BepInEx is needed.

## Settings

`BepInEx\config\mdbailey94.wol.ascension.cfg`: `Enabled`, `Level` (0–10, the prompt's starting level), `ShowLevelInRun`.

## Troubleshooting

`BepInEx\LogOutput.log` records each portal ("Portal to '...' (starts a run: true)"),
"Starting run at Ascension n", "Left the trials", and every level load.
