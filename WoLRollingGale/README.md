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

## 0.6.0: balance settings (asked for by Matt)

New settings in `BepInEx\config\mdbailey94.wol.rollinggale.cfg` (or the Mods menu). The defaults
are the values the arcana already had, so nothing changes until you edit them.

- **[Cast]** (next cast): `FirstBurstDistance` 2.5, `BurstSpacing` 3, `FirstBurstScale` 2.5,
  `BurstScaleGrowth` 0.5, `BurstInterval` 0.12 s, `EchoScale` 0.85, `BurstCount` 3.
- **[Enhanced]** (next cast): `BurstCount` 4.
- **[Signature]** (next cast): `FanBursts` 6, `FanSpread` 40 degrees, `FanDelay` 0.1 s,
  `FanInterval` 0.13 s, `FanFirstDistance` 2.5, `FanDistanceStep` 1, `FanFirstScale` 2.6,
  `FanScaleStep` 0.35, `FanHitStopFrames` 2, `GatherTime` 0.3 s, `FinalDistance` 5.5,
  `FinalScale` 7, `FinalHitStopFrames` 5, `FinalSlowMoTime` 0.35 s, `FinalSlowMoSpeed` 0.05.
- **[Balance]** (after restarting the game): `BurstDamage` 8, `EchoDamage` 3, `FanBurstDamage` 12,
  `FinalGaleDamage` 45, `GaleCooldown` 5 s, `BurstPull` 22, `FanPull` 26, `FinalPull` 36,
  `BurstHitStun` 1.1, `FanHitStun` 1.2, `FinalHitStun` 1.6, `RegrabTime` 0.1 s.

## 0.5.0: a cinematic signature (asked for by Matt)

Rolling Gale has a **signature version** now (the spellbook's Signature page; unlocked along with
the arcana, `[General] SignatureUnlocksWithArcana`):

1. **Fan**: six bursts sweep out across 80 degrees, from one side of your aim to the other, each
   further out (2.5 to 7.5), bigger and harder (12 each, pulling harder) and echoing the one
   before. Each lands with a 2-frame hit-stop and a shake. The camera pulls back 18% and the
   room darkens to 60%.
2. **Gather**: a breath where the room dims further and the air rushes into the fan's middle.
3. **Final gale**: a huge burst (scale 7) at the fan's middle drags everything in for 45, with a
   5-frame hit-stop, a white flash, a heavy shake and a moment of near-frozen time. Then the
   camera and light ease back.

An enemy dragged through all of it takes about 130. A dash cancels it; the camera and light
always go back (`Rolling Gale: signature restored (...)`).

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

Damage, cooldown, pull, hit stun, burst spacing, size, timing and count are settings (see 0.6.0
above; defaults in `RollingGalePlugin.cs`). The look (camera, flash, shake, drawn effects) is the
constants in `RollingGaleState.cs` and `GaleLook.cs`.

## Building it yourself

Put `Assembly-CSharp.dll`, `UnityEngine.dll` (from `WizardOfLegend_Data\Managed`), `BepInEx.dll`
(from `BepInEx\core`) and `LegendApi.dll` into a `lib` folder at the repo root, then run
`dotnet build -c Release`.


## 0.6.1

Signature damage retains its signature flag through bursts and delayed hits, so it cannot generate signature charge. Standard/enhanced casts still charge normally.
