# Rolling Gale (Wizard of Legend)

A new Air arcana, added with [LegendAPI](https://github.com/yekoc/LegendAPI).

> **Rolling Gale**: Send a line of wind bursts rolling forward, dragging enemies along with them!
> **Enhanced**: Adds a fourth, larger burst!

| Stat | Value |
|------|-------|
| Element | Air |
| Tier | 2 |
| Damage | 8 per burst, 3 per echo pulse (up to 33 per enemy; 44 enhanced) |
| Cooldown | 5 s |
| Bursts | 3 (4 when enhanced) |

Each burst lands further out along your aim and is a little larger than the last, then pulses
again a moment later. Bursts pull enemies inward, like Gust Burst does, and each one can grab an
enemy again, so they get dragged along the whole line.

## Look

Built from the game's own wind art, on top of its air vortex, wind burst and dust:

- **Cast**: the game's dash air burst and air shockwave at your hands, and its large wind slash
  rolling off along your aim.
- **Travel**: three of the game's wind slashes rolling from each burst to the next, with its wind trail.
- **Each burst**: an inward spiral on the floor (8 frames: it forms from the rim, its three brushed
  arms draw in, then it collapses into the eye), painted in the game's three wind tones with no
  outline; the game's air-spin curl winding up at its eye; the game's dust puffs and rocks, leaves
  and wind wisps spiralling in; and its dust suction. Echo pulses get a smaller, quicker spiral.
- **End**: the game's wind twirl and a wind puff breaking into curls over the last burst, with its
  spinning air bursts.

If any of the game's sprites isn't loaded, a drawn stand-in in the same style plays instead. Visual
only: hits, timing and pull are unchanged. The log says `Rolling Gale: using the game's ...` (or
`... isn't loaded, drawing ...`) once per sprite set, and `Rolling Gale: visual '<name>' ready (...)`
the first time each effect plays, or which one it skipped.

## Install

1. Install BepInExPack of Legend, **LegendAPI** and **HookGenPatcher** (all on Thunderstore).
   Without HookGenPatcher, LegendAPI fails to load with a `TypeLoadException`.
2. Download `WoLRollingGale.zip` from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest)
   and unzip it into your Wizard of Legend folder.
3. Buy it from the arcana shop in the Plaza, or check it in the spell book.

## Tuning

Damage, cooldown and pull strength are in `RollingGalePlugin.cs` (`skillStats`). Burst spacing,
size, timing and count are the constants at the top of `RollingGaleState.cs`.

## Building it yourself

Put `Assembly-CSharp.dll`, `UnityEngine.dll` (from `WizardOfLegend_Data\Managed`), `BepInEx.dll`
(from `BepInEx\core`) and `LegendApi.dll` into a `lib` folder at the repo root, then run
`dotnet build -c Release`.
