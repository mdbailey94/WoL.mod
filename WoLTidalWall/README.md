# Tidal Wall (Wizard of Legend)

A new **Water standard arcana**, added with [LegendAPI](https://github.com/yekoc/LegendAPI).

> **Tidal Wall**: Raise a wall of water that surges forward, washing enemies away and sometimes
> freezing them!
>
> *Enhanced: The wave is wider and surges further!*

| Stat | Value |
|------|-------|
| Element | Water |
| Tier | 2 (like Tidal Blast) |
| Price | 6 (like Tidal Blast) |
| Damage | 10 per row, 20 for the crest (up to 50 per enemy carried the whole way; 60 enhanced) |
| Knockback | 30 per row, 50 for the crest, always straight along your aim |
| Freeze | 15% chance per row, 50% for the crest, 1.5 s |
| Cooldown | 6 s |
| Wave | 4 rows, 3 wide (5 rows, 5 wide when enhanced) |

A wall of water rises in front of you and rolls forward: each row of water swells up, then bursts
a tenth of a second later as the next row rises a step further out, so the wave keeps shoving
whatever it catches the same way, straight along your aim. The last row is a bigger **crest** that
hits harder and throws enemies further. The water is the game's own Tidal Blast water (the orbs
and their deluge burst), so it looks and sounds like a native Water arcana.

Freeze is the game's Water status effect (Air's is Slow). Plain water arcana like Tidal Blast
don't freeze at all and ice ones always do, so Tidal Wall has a chance. A lane of the wave that
runs into a wall stops there instead of passing through it. Dash to cancel; any water that hasn't
burst yet drains away.

## Checking it works

`BepInEx\LogOutput.log` shows `Tidal Wall 0.1.0 registered` at start-up, then for each cast:

```
Tidal Wall: cast, 4 rows x 3 wide toward (1.00, 0.00)
Tidal Wall: using the game's Tidal Blast water orbs (hit area ... )   <- first cast only
Tidal Wall: wave done, 12 bursts, 5 hits on 2 targets, 1 frozen
```

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLTidalWall.zip` from
the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it into
your Wizard of Legend folder. It shows up in the arcana shop in the Plaza (or straight away with
Unlock Mod Content on).

## Tuning

Damage, knockback, freeze chance and cooldown are in `TidalWallPlugin.cs` (`skillStats`). Rows,
width, spacing, timing and sizes are the constants at the top of `TidalWallState.cs`.

## Building it yourself

Put `Assembly-CSharp.dll`, `UnityEngine.dll` (from `WizardOfLegend_Data\Managed`), `BepInEx.dll`
(from `BepInEx\core`) and `LegendApi.dll` into a `lib` folder at the repo root, then run
`dotnet build -c Release`.
