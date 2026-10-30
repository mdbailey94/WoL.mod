# Cyclone (Wizard of Legend)

A new **Air standard arcana**, added with [LegendAPI](https://github.com/yekoc/LegendAPI).

> **Cyclone**: Hold to whip up a twister you can steer, growing into a raging hurricane that
> bursts, blowing everyone away!
>
> *Enhanced: Reaches full strength sooner and bursts wider!*

- **Hold** the button: a tiny twister spins up about **three tiles in
  front** of you while you stay planted, channeling. **Steer it** by pushing a direction (it stays put
  when you don't): quick while it's small, heavier as it grows, and barely moving from halfway on;
  never through walls, and up to 9 away from you.
- Over **4 seconds** it grows into a **hurricane**: the twister swells, the air boss's storm
  vortex (dust, debris, light streaks) builds up around it, layered swirls widen, dust streams in,
  debris and leaves whirl round it, and a rumble grows into a shake with gusts bursting out. As it grows it hits **faster** (every
  0.45 s down to 0.12 s) and **harder** (2, 3, 4, then 6 damage), tugging enemies inward.
- **Let go** early and it dies down. **Hold on one more second** at full power and it **bursts**:
  14 damage and a huge knockback that throws everyone around it away.
- **Enhanced**: full size in 3 seconds (bursting at 4), and a wider burst.
- Dash to cancel at any time. Cooldown 5 seconds.

## 0.7.0: more settings to balance with (asked for by Matt)

The numbers that were fixed in the code are now settings in
`BepInEx\config\mdbailey94.wol.cyclone.cfg`. Their defaults are the old values, so nothing plays
differently until you change one. All are new names; the existing settings are unchanged.

- `[Cast]` (next cast): `GrowTime`, `FullPowerTime`, `MinHoldTime`, `StartDistance`, `SteerSpeed`,
  `HeavySteerSpeed`, `HeavyAt`, `SteerRange`, `StartHitSize`, `FullHitSize`, `SlowHitInterval`,
  `FastHitInterval`, `BurstSize`.
- `[Enhanced]` (next cast): `GrowTime`, `FullPowerTime`, `SteerSpeed`, `HeavySteerSpeed`, `HeavyAt`,
  `BurstSize`.
- `[Signature]` (next cast): `TossFrom`, `TossReach`, `TossAgainAfter`, `CameraPullBack`, `RoomDim`,
  `LightningFrom`, `LightningFlash`, `ThrowSlowMoTime`, `ThrowSlowMoSpeed`, `BurstHitStopFrames`,
  `BurstFlash`, `BurstShake`, `BurstSlowMoTime`, `BurstSlowMoSpeed`, `BurstEaseBack`, `LetGoEaseBack`.
- `[Balance]` (after restarting the game): `Stage1Damage` to `Stage4Damage`, `BurstDamage`,
  `Stage1Knockback` to `Stage4Knockback` (negative pulls in), `BurstKnockback`, `TwisterHitStun`,
  `BurstHitStun`, `SameAttackImmunity`, `Cooldown`.

## 0.6.0: higher throws, and the signature no longer refills its own gauge (asked for by Matt)

- The signature throws enemies **11 units** high (was 2.5; `[Signature] TossHeight`, up to 16)
  and they stay up longer the higher they go (about 1.9 s at 11). Each lands for **25** (was 12;
  `TossDamage`) with a bigger crash of dust and a shake. Old values in a config file are moved up once.
- The signature's hits (the twister, the burst and the landings) are marked as a signature's,
  as the game marks its own, so they no longer refill the signature gauge. Up to 0.5.2 its many
  hits filled it again almost at once.

## 0.5.2: a cinematic signature (asked for by Matt)

The signature twister now plays out like the other mods' signatures (Thunderhead, Tidal Wall):

1. **Cast**: the game's own signature freeze and burst as the gauge is spent.
2. **Storm builds**: as it grows, the camera pulls back up to 25% and the room darkens to half
   its light. From a third of the way in, **lightning flickers** in the storm: the room flashes
   for a moment with a small shake, more often as it grows.
3. **Throw**: the first enemy it throws hangs at the top of its arc in a moment of **slow
   motion** (0.35 s, once per cast).
4. **Burst**: a 5-frame hit-stop, a white flash, a heavy shake and time all but stopping
   (0.45 s), then the camera and light ease back over 0.9 s. Let go early and they ease back
   over 0.5 s.

The camera and light always go back: when it ends, when it's cut short (a dash, a hit, a
level load), and in any case after 9 s (logged as `Cyclone: signature restored (...)`).

## 0.5.1

- The signature twister is **2.5x** the enhanced one's width (was 1.8x; `[Signature] Width`, now
  up to 4). A config file still holding 0.5.0's 1.8 is moved to 2.5 once.

## 0.5.0: three tiers and a signature (asked for by Matt)

- **Standard**: as before, but the wind carries only dust and rocks, no leaves.
- **Enhanced**: leaves whirl round it, it steers faster (5.5 instead of 3.5 while small) and
  keeps moving when grown (heavy only from three quarters of the growth, 0.8 instead of 0.15),
  and it charges quicker: full size in 3 s, bursting 0.6 s later (standard: 4 s and 1 s).
- **Signature** (the signature slot; it has everything enhanced has): the twister is **1.8x
  wider** (hit area, burst and look; `[Signature] Width`), and from early in its growth it
  **throws every enemy it catches up into the air**, bosses and the floor boss excepted. A thrown
  enemy is held where it was caught while its picture rises 2.5 units and falls over 0.9 s
  (`TossHeight`). When it lands it takes 12 damage (`TossDamage`) with a puff of dust and a
  small shake; it can be thrown again half a second after landing.
- The signature is unlocked along with the arcana (`[General] SignatureUnlocksWithArcana`, on;
  off: find its Signature drop in a run, as for the game's own arcana).
- The log says `Cyclone: cast (standard|enhanced|signature...)`, and how many enemies the
  signature threw when it bursts or is let go.

## Look

The twister is pixel art in the game's own wind style, at the game's pixel size:

- **Funnel** (0.4.4): a **hand-drawn twister** in three sizes (12, 32 and 48 px wide, six
  frames each, drawn for the mod in the game's wind tones), the largest that fits under the
  funnel's current width scaled up the rest of the way, tinted to gale grey and storm slate as
  it grows, cut into slices that lean with the sway. `[Look] Twister = Coded` uses the 0.4.3
  funnel instead: one tapered twister, narrow at the ground and flaring open at the top, its
  body wound with spiral stripes that wrap round it and scroll (a 6-frame loop), lit from the
  upper left with its right side in shade and the dark throat showing in the open mouth; the
  game's three wind tones, no outline. It grows taller and wider and darkens in two steps from the
  game's wind teal to gale grey to storm slate, sways like a snaking tornado (the picture is cut
  into slices that lean with it), with the game's wind wisps peeling off its sides. Up to 0.4.2
  it was a stack of spinning gust rings.
- **Debris**: the game's dust puffs and rocks, and leaves in its leaf green, orbiting it, more and
  faster as it grows, at a radius that grows with it, passing behind it and in front.
- **Dust ring**: a ring of dust puffs churning round its base.
- **Endings**: let go and it collapses, dropping its debris; burst and it blows apart in the game's
  spinning gust ring (AirSpinnerBurst) with its wind slashes racing out and the debris flung away.
  Any of the game's sprites that isn't loaded is replaced by a drawn stand-in in the same style.

Around it are the game's own effects: the air boss's storm vortex (dust, debris, light streaks)
building up, layered air swirls, dust streaming in, and gusts near full power. They're stripped
down to just their looks (no scripts, so nothing of the originals can hurt anyone or vanish early).
The hits come from invisible wind bursts, unchanged. If the drawn funnel can't be made, the game's
Twister arcana tornado stands in. The log says `Cyclone: visual '<name>' ready (...)` the first time
each piece plays, `Cyclone: using the game's ...` once per sprite set it borrows (or `... isn't
loaded, drawing ...`), `Cyclone: using the game's storm vortex`, and which (if any) it skipped.

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLCyclone.zip` from the
[latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it into your
Wizard of Legend folder. It shows up in the arcana shop (or straight away with Unlock Mod Content on).
