# Bramble Ring (Wizard of Legend)

A new **Earth standard arcana**, added with [LegendAPI](https://github.com/yekoc/LegendAPI).

> **Bramble Ring**: Burst a ring of thorns out of the ground around you, slowing the enemies they catch!
>
> *Enhanced: A second, wider ring bursts out a moment later!*

| Stat | Value |
|------|-------|
| Element | Earth |
| Tier | 4 (like Terra Ring) |
| Price | 6 (like Terra Ring) |
| Damage | 25 (enhanced outer ring: 20 more) |
| Status | Slow: 50% move speed for 2 s |
| Cooldown | 5 s |
| Reach | about 1 to 3 units around you (enhanced: and 3 to 5) |

Your wizard slams the ground (Terra Ring's slam) and thorns burst up all around them in two
staggered rows, the inner row first so the ring ripples outward, throwing up rock and leaves. The
thorns are the game's own earth spikes, the ones Earth Wave and the earth enemies' spike waves
raise, each with its own hit box. Each ring hits an enemy once, shoves it a little away from you
and slows it. No thorns are raised inside walls or over pits.

**Enhanced**, a second, wider ring bursts out from the same spot 0.35 s later. Enemies the first
ring shoved outward land right in it.

## Log

`BepInEx\LogOutput.log` shows:

- `Bramble Ring 0.1.0 registered` at start-up.
- `Bramble Ring cast at (x, y)` (`, enhanced` when it is) each cast.
- `Bramble Ring: ring 1 raised 24 thorns 1.4-2.4 out` for each ring, with how many were skipped
  in walls or pits.
- `Bramble Ring: ring 1 hit 3 enemies (...), 3 slowed` (or `hit no enemies`) once each ring is
  done. Enemies that died, and Air enemies (which can't be slowed), don't count as slowed.
- `Bramble Ring: thorn hit box is a ...` once, the size of one thorn's hit box.

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLBrambleRing.zip`
from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it
into your Wizard of Legend folder. It shows up in the arcana shop (or straight away with Unlock
Mod Content on).

## Tuning

Damage, cooldown, knockback and the slow are in `BrambleRingPlugin.cs` (`skillStats`). Ring sizes,
thorn spacing and timing are the constants at the top of `BrambleRingState.cs`.

## Building it yourself

Put `Assembly-CSharp.dll`, `UnityEngine.dll` (from `WizardOfLegend_Data\Managed`), `BepInEx.dll`
(from `BepInEx\core`) and `LegendApi.dll` into a `lib` folder at the repo root, then run
`dotnet build -c Release`.
