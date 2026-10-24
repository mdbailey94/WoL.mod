# Bramble Ring (Wizard of Legend)

A new **Earth standard arcana**, added with [LegendAPI](https://github.com/yekoc/LegendAPI).

> **Bramble Ring**: Knit a thicket of thorny vines around you that snares the enemies inside and
> pricks them while it stands!
>
> *Enhanced: The thicket stands longer and spreads outward into a wider ring!*

| Stat | Value |
|------|-------|
| Element | Earth |
| Tier | 4 (like Rippling Vines and Terra Ring) |
| Price | 6 (like Rippling Vines and Terra Ring) |
| Damage | 5 for the catch, then 3 a prick every 0.5 s: 20 over the full 3 s (enhanced 29 over 4.5 s). 0.2.1 trimmed the pricks from 5 (30 a full stand): a hard snare for the whole stand shouldn't also out-damage Embracing Vines (25) |
| Control | Snare: the game's root (held in place, no knockback), renewed by every prick, 1 s past the last. Bosses are slowed instead (50% for 2 s) |
| Cooldown | 6 s |
| Reach | about 3 units around where it was cast (enhanced: 4) |

Your wizard slams the ground (Rippling Vines' slam) and thorny vines knit a thicket around them,
one after another round the ring, in two rows about 1.5 and 2.5 units out. The vines are the
game's own vine traps, the ones Rippling Vines and the Root Shield relic raise, each with its own
grabbing hit box. The thicket stands where it was cast for **3 seconds** while you walk off (or
stay inside it, nothing in it hurts you).

Every half second, every enemy inside the thicket is **pricked** by the thorns (5 for the first
prick, 3 for each after it, leaves fly) and **snared** with the game's root status: held where it stands, ignoring knockback, until a
second after its last prick. Enemies that walk into the thicket are caught the same way, and a vine
whose hit box they cross grabs them and holds them on the spot as well. The first prick on an enemy
(the catch) makes it flinch and also slows it; the pricks after that don't flinch, so a snared enemy
can still fight back from inside. Bosses, and anything else the game won't let a grab hold, aren't
snared: they get the catch (and its slow) on every prick instead.

**Enhanced**, the thicket stands **4.5 seconds**, and a moment after the first two rows a third,
wider row of vines spreads out about 3.5 units from the centre, so everything within 4 units is
pricked.

## Log

`BepInEx\LogOutput.log` shows:

- `Bramble Ring 0.2.1 registered` at start-up.
- `Bramble Ring: cast at (x, y)` (`, enhanced` when it is) each cast.
- `Bramble Ring: knitted 18 vines in 2 rows 1.5-2.5 out` once the rows are up, with how many spots
  were skipped in walls or pits; enhanced, `Bramble Ring: vines spread outward: 16 more at 3.5 out`.
- `Bramble Ring: caught EarthArcher(Clone), snared` for each enemy the first time it's pricked
  (`in a vine` when a vine grabbed it first). Instead of `snared` you may see
  `slowed (can't be snared)` (bosses), `hit only` (it resisted the status) or `killed`.
- `Bramble Ring: thicket withered after 3.0 s: caught 3 (...), 1 of them by a vine first (2 vine
  hits in all), 15 pricks for 51 damage before armour` (or `caught nothing`) when it's gone.
- `Bramble Ring: vine hit box is a ...` once, the size and layer of one vine's hit box.
- `Bramble Ring: effect '...' unavailable, skipping it` if a cosmetic effect can't be made.

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLBrambleRing.zip`
from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it
into your Wizard of Legend folder. It shows up in the arcana shop (or straight away with Unlock
Mod Content on).

## Tuning

Damage, cooldown, the snare and the slow are in `BrambleRingPlugin.cs` (`skillStats`). The ring
sizes, how long it stands, the prick interval and the knitting speed are the constants at the top
of `BrambleRingState.cs`; how the vines are raised is in `BrambleVines.cs`.

## Building it yourself

Put `Assembly-CSharp.dll`, `UnityEngine.dll` (from `WizardOfLegend_Data\Managed`), `BepInEx.dll`
(from `BepInEx\core`) and `LegendApi.dll` into a `lib` folder at the repo root, then run
`dotnet build -c Release`.
