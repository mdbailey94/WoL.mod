# Tidal Wall (Wizard of Legend)

A new **Water standard arcana**, added with [LegendAPI](https://github.com/yekoc/LegendAPI). It has
a **signature version**, so it can go in the signature slot.

> **Tidal Wall**: Raise a wall of water that blocks projectiles, then rolls forward sweeping enemies
> along until it crashes!
>
> *A wider wall that rolls further. Signature: a towering tidal wave whose crash freezes all it carries!*

| Stat | Value |
|------|-------|
| Element | Water |
| Tier | 2 |
| Price | 6 |
| Cooldown | 6 s (Tidal Blast 6, Whirlpool 7) |
| Wall | 5 water columns, 3.5 wide, rises 2 in front of you, stands 0.2 s, then rolls 4.8 at 6/s |
| Enhanced | 7 columns (4.9 wide), rolls 6.3 |
| Damage | 4 per push (a push every 0.35 s while it rolls), 6 for the crash: **14** for an enemy carried the whole way (18 enhanced) |
| Knockback | 10 per push, 20 for the crash, always straight along your aim |
| Freeze | none |
| Projectiles | enemy projectiles that touch the wall are stopped (or bounced back) |
| Signature | 8 columns (10 enhanced) 1.7x as tall, 8 wide (10), rushes 12 (14.4) at 12/s, 10 per push every 0.25 s and **60** for the crash, which **freezes** everything for 2.5 s: up to 100 per enemy (about 105 enhanced); knockback 15 per push, 30 for the crash |

0.4.4 (Matt: too wide, pushes too far) narrows every level by about a quarter (columns 0.7
apart, 1.0 in the signature, from 0.9 and 1.3), shortens every roll by a fifth and halves all
the knockback (was 20 / 40, signature 30 / 60). A shorter roll means one push fewer, so a
carried enemy takes a little less damage.

A single wall of water rises across your aim a short way in front of you, stands for a moment, then
rolls slowly forward as one front. Everything it touches is shoved along with it, pushed again
every 0.35 s, so enemies get carried rather than smashed, and at the end of its run (or when it
meets a level wall) it crashes in a splash that throws them a little further. While it stands and
rolls it also stops enemy projectiles. It's control, not damage: the pushes are light and the
wall never freezes.

The wall is the game's own water wall (a Water Wall the game has but never lets you cast), with its
projectile barrier; the pushing is one hit area across the whole wall. Once it's cast it runs by
itself, so you're free to move or cast again straight away.

0.2.0's version was rows of Tidal Blast's water orbs bursting one after another (up to 44 per enemy,
with a freeze chance). That was too close to Tidal Blast and too strong for a standard arcana.

### Look

0.4.0 gives the normal and enhanced wall its own drawn pixel-art animations on top of the game's
water effects (looks only: damage, timing and reach are unchanged; the signature keeps its own
cinematic look). A jet of water bursts up as each column rises, with a ripple spreading over the
floor; a foam crest curls along the top of every column while it stands and rolls; the front flings
droplets that splat where they land and fans of spray; the wave leaves a wet trail of puddles that
glint and dry up; every swept enemy is churned in a ring of swirling, bubbling foam; and the crash
throws up a crown of water at each burst, with mist hanging over it, foam washing out over the floor
and droplets raining down. Each drawn visual logs `Tidal Wall: visual '...' ready (...)` the first
time it plays, and each wave logs `Tidal Wall: look: ...` with what it drew.

0.4.1 restyles it to match the game's own water art: the splash crowns, droplets, bubbles and mist
are now the game's own sprite frames (WaterHitSmall, WaterHitEffect, WaterParticles, BubblePop,
Smoke), and the rest (crest, puddles, churning foam, ripple, crash foam) is repainted in colours
sampled from the game's water sheets, with no outlines, fading by alpha. If one of those game sheets
isn't loaded, a painted stand-in is used and the log says so once.

0.4.2 redraws the crest: bigger (32x20 instead of 24x14) and wide enough that neighbouring
columns' crests overlap into one line of water, all moving in step rather than staggered, and
turned to the roll direction (snapped to 15 degrees) so a wave rolling up, down or diagonally
keeps its crest aligned with the way it's going.

## Signature

Put Tidal Wall in the **signature slot** (the spellbook in your house lists it under Signature
Arcana) and cast it with the signature gauge full: the wall becomes a **towering tidal wave**, in four
beats:

1. **Gather** (0.6 s): you raise your arms and the water streams in behind you with a rumble; the
   camera pulls back 18% and the room darkens.
2. **Rise**: the wave erupts, columns 1.7x as tall, with a hit-stop and a heavy shake.
3. **Rush**: it races 15 units at 12/s (18 when also enhanced), and every push that lands hits with a
   short hit-stop and a shake.
4. **Crash**: time all but stops and eases back, the room flashes, a row of huge splashes and ice
   bursts, and everything it carried is **frozen** for 2.5 s. The camera and light ease back to
   normal.

The gather can't be dash-cancelled (the gauge is already spent); getting hurt or killed during it
cancels the wave. The camera and light always go back: when the crash finishes, when a gather is
cancelled, when the wave goes with a level load, and if all else fails after a few seconds anyway.
Hit-stops and slow motion are the game's own and end by themselves. As with the game's own arcana
the cast spends the whole gauge and its hits don't refill it.

The signature version is **unlocked along with the arcana** (buy it once and it's in the Signature
list). If you'd rather earn it the vanilla way, finding its Signature drop in a run, turn
`SignatureUnlocksWithArcana` off in `BepInEx\config\mdbailey94.wol.tidalwall.cfg`.

## Checking it works

`BepInEx\LogOutput.log` shows `Tidal Wall 0.3.1 registered` at start-up. A normal cast logs:

```
Tidal Wall: cast (normal), wall 4.5 wide (5 columns), rolls 6.0 at 6.0/s (1.00 s), a push every 0.35 s, toward (1.00, 0.00)
Tidal Wall: hit area is the game's attack box (...)                                <- first cast only
Tidal Wall: the game's WaterWall: 5 of 5 columns up (centred by ...), ...          <- first cast only
Tidal Wall: normal wave crashed at the end of its run after rolling 6.0 of 6.0: swept 2 enemies, 7 hits, 30 damage, 0 frozen, 1 projectiles blocked (5 of 5 water columns)
```

The first line says which version ran: `normal`, `enhanced`, `signature` or
`both: enhanced + signature`. A wave that runs into a wall says `broke on a wall` instead. A
signature cast adds a line per beat and one when everything is put back:

```
Tidal Wall: signature beat 1/4 gather: 0.60 s, camera pulls back 18% (size ... -> ...), stage light ... -> ..., rumble
Tidal Wall: signature beat 2/4 rise: 10 columns x1.7 tall, 13.0 wide, hit-stop 4 frames, shake 1.2
Tidal Wall: signature beat 3/4 rush: 18.0 at 12.0/s, a push every 0.25 s with hit-stop 2 frames on contact
Tidal Wall: signature beat 4/4 crash (crashed at the end of its run) after 18.0: slow motion 0.45 s from x0.03, ...
Tidal Wall: signature restored (crash finished): camera size back to ..., light back to ...
Tidal Wall: both: enhanced + signature wave crashed at the end of its run ...: swept 3 enemies, ..., 3 frozen, ...
```

When the signature is unlocked you get `Tidal Wall: signature unlocked along with the arcana ...`
once. If one of the game's effects can't be used you get
`Tidal Wall: effect '...' unavailable, skipping it` once and the cast carries on without it. The
gauge fills by dealing damage; the training dummies in your house work for that (they count as
enemies in the log).

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLTidalWall.zip` from
the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it into
your Wizard of Legend folder. It shows up in the arcana shop in the Plaza (or straight away with
Unlock Mod Content on).

## Tuning

Damage, knockback, freeze and cooldown are in `TidalWallPlugin.cs` (`skillStats`; levels 1 and 2 are
the wall's pushes and crash, 3 and 4 the signature's). Width, speed, distance, timing and the
signature's gather are the constants at the top of `TidalWallState.cs`; the hit-stops, shakes,
slow motion, camera pull-back and light are at the top of `WaveFront.cs`.

## Building it yourself

Put `Assembly-CSharp.dll`, `UnityEngine.dll` (from `WizardOfLegend_Data\Managed`), `BepInEx.dll`
and `0Harmony.dll` (from `BepInEx\core`) and `LegendApi.dll` into a `lib` folder at the repo root,
then run `dotnet build -c Release`.
