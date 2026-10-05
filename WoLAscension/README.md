# Ascension (Wizard of Legend)

Ascension levels 1–10 for harder runs. Pick your level in the hub; each level adds a modifier on
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

## Choosing a level

When you step into the portal that starts a run, the game pauses and an **ASCENSION** prompt
appears, drawn in the game's pixel style with the game's own font:

- **Left / right** (either stick, the d-pad, arrow keys, or click the arrows): change the level.
  The pips fill up and the list shows what that level adds.
- **A** (or Enter / Space): begin the run at that level.

It starts at the level you picked last time. Level 0 (OFF) is normal difficulty. The level is
locked for the run, a small "ASCENSION n" tag shows in the corner, and all modifiers come off when
you're back in the hub. The modifiers use the game's own stat system, so they stack normally with
relics. You can also turn the whole mod off in the title screen Mods menu.

If a run starts from somewhere the portal hook doesn't catch, the prompt appears (paused) as soon
as the first enemies do.

## Install

Download `WoLAscension.zip` from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest)
and unzip it into your Wizard of Legend folder. Only BepInEx is needed.

## Settings

`BepInEx\config\mdbailey94.wol.ascension.cfg`: `Enabled`, `Level` (0–10, the prompt's starting level), `ShowLevelInRun`.

## Troubleshooting

`BepInEx\LogOutput.log` records each portal ("Portal to '...' (starts a run: true)"),
"Starting run at Ascension n", "Entered the hub", and every level load.
