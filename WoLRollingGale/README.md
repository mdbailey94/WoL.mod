# Rolling Gale (Wizard of Legend)

A new Air arcana, added with [LegendAPI](https://github.com/yekoc/LegendAPI).

> **Rolling Gale**: Send a line of wind bursts rolling forward, dragging enemies along with them!
> **Enhanced**: Adds a fourth, larger burst!

| Stat | Value |
|------|-------|
| Element | Air |
| Tier | 2 |
| Damage | 12 per burst |
| Cooldown | 5 s |
| Bursts | 3 (4 when enhanced) |

Each burst lands further out along your aim and is a little larger than the last. Bursts
pull enemies inward, like Gust Burst does.

## Install

1. Install BepInExPack of Legend and **LegendAPI** (both on Thunderstore).
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
