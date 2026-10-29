# Stone Charge (Wizard of Legend)

A new **Earth standard arcana**, added with [LegendAPI](https://github.com/yekoc/LegendAPI).

> **Stone Charge**: Hurl a great stone shield forward: it hangs in the air for a moment, then
> slams down, blasting away every foe around it!
>
> *Enhanced: A bigger shield that drops sooner and blasts wider!*

## 0.6.0: a thrown shield that slams down (asked for by Matt)

Stone Charge is no longer a charge. **Cast** it and the wizard throws a big stone shield (the
game's Aegis Charge slab, 1.6 times its size) **5 units forward** in an arc (short of a wall in
the way). It hangs in the air over where it'll land for **0.45 s**, rising and trembling while
dust and stones stir on the ground below (the tell), then **slams down**: an earth blast of
**radius 2.6** for **22** that throws enemies outward, with a crater, rock spikes and flung
stones. The wizard is free again after a 0.22 s throw (a dash cancels it; the shield flies on).

- **Enhanced**: the shield is 1.25 times bigger, hangs a quarter less, and blasts at radius 3.2 for 28.
- **Signature**: a **colossal shield** (4.5 times the game's) with burning cracks, thrown 6.5
  units. While it's up the camera pulls back and the room dims; it hangs 0.8 s, rising 6 units,
  then slams at **radius 7 for 60** with a hit-stop, a flash, a heavy shake and half a second of
  near-frozen time. A **shockwave** rolls out to radius 11 for 20 more, then the camera and light
  ease back. Its hits don't refill the signature gauge (as the game's own signatures).

| Stat | Value |
|------|-------|
| Element | Earth |
| Tier | 4 (like Aegis Charge and Terra Ring) |
| Price | 6 (like Aegis Charge and Terra Ring) |
| Damage | Shove 3 as it leaves your hands; slam 22 (enhanced 28, signature 60); signature shockwave 20 |
| Knockback | Slam 35 outward (enhanced 40, signature 60); shockwave 45 |
| Cooldown | 6 s |

Settings (`mdbailey94.wol.stonecharge.cfg`): `[Throw]` Distance, FlightTime, SlamDelay,
ShieldSize, SlamRadius, EnhancedSlamRadius; `[Signature]` ThrowDistance, SlamDelay, ShieldSize,
SlamRadius; `[Balance]` RaiseDamage, RaiseKnockback, SlamDamage, EnhancedSlamDamage,
SignatureSlamDamage, SignatureShockwaveDamage, SlamKnockback, ThrowCooldown (after a restart).
The charge's old settings stay in the file unused.

Everything below describes the charge it was up to 0.5.1.

## Signature: the phalanx (0.5.0; 0.5.1 made it a phalanx, asked for by Matt)

Stone Charge has a **signature version** (the spellbook's Signature page; unlocked along with
the arcana, `[General] SignatureUnlocksWithArcana`):

1. **The phalanx forms**: the shield goes up fully charged (runes burning, rocks circling), and
   a **line of stone shields** forms out to either side of it, a pair at a time from the middle
   outward, each pair landing with a shake: **7 shields, about 10 units wide**, with a hit-stop
   and a flash when the line is complete. The camera pulls back 25% to take it in and the room
   darkens.
2. **Advance**: the whole line moves and turns with you. Running it into enemies doesn't
   crash: **every shield grabs** what it touches and the line carries them along in front of it,
   up to 8 (bosses and the floor boss excepted; one a wall catches behind is let go). Press
   again or let the time run out to unload.
3. **Crash**: **every shield crashes at once** where it stands (radius 1.9 each, `[Signature]
   CrashScale`), for 42 (`[Balance] SignatureCrashDamage` 7 at full force) with a 6-frame
   hit-stop, a flash, a heavy shake and half a second of near-frozen time.
4. **Shockwave**: 0.3 s later the ground erupts again a little ahead of every shield, with rock
   spikes, for 15 more (`SignatureAftershockDamage`), throwing enemies outward; then the camera
   and light ease back.

An enemy carried into it takes about 70 (the bash, the crash and the shockwave). The camera
and light always go back (`Stone Charge: signature restored (...)`). 0.5.0's signature was a
single shield whose crash was one big earthquake.

**Cast** it and your wizard slams the ground and a slab of stone (the game's own Aegis Charge
shield) rises a unit in front of them, shoving back whatever's standing there. Then, for up to
**4 seconds**, you run about freely behind it (it turns to face the way you run) and **every moment
on the move stores force** in it, in proportion to your run speed against its base: running at base
speed stores 25% a second (full in 4 s), a sprint (+40%, which you work up to after a second on the
move, since the game drops its own sprint when you cast) stores it in about 2.9 s, and speed relics,
the cloak and speed buffs count too. Standing still stores nothing. Rock debris gathers round you as
it builds, the ground cracks behind you from half force, and a rock aura swirls at full.

Behind the shield, hits coming from in front do **30% less** and nothing staggers or knocks you
back, but you still take the damage and you can't cast anything else.

**A dash keeps the shield and the force** (0.3.0). It's your normal dash, played without leaving
the shield: the same distance and speed (your dash's duration and the dash speed stats, so dash
relics count), animation, air burst, dust, trails and sound, over pits the way a dash goes over
them, then the usual quarter-second slide before you can dash again (or a chained dash with the
skip-the-slide relic). The shield turns to the way you dash. The dash itself stores no force (its
speed is forced, not your run), and it doesn't make your dash arcana's attack, but **dash the shield
into an enemy** (during the dash or the slide after it) and it crashes on the spot with the force
you had. Only the slam before the shield is up can still be dashed out of (cancelling the cast).

**Run the shield into an enemy** or **press the button again** (or let the 4 seconds run out) to
**unload** with whatever force is stored by then (0.2.0: running into an enemy crashes at once,
anything within 0.9 of the shield's spot while you're on the move): a short rush forward of
2.5 units, bashing with the shield on the way (level 2) and crashing where it ends (level 3), both
shoving enemies along your line. Their damage and knockback scale with the stored force: an instant
crash is weak (2 + 4 damage, knockback 10), a crash after a long sprint is not (12 + 24, knockback
70, and a longer stun from 75% force up). So the big hit costs you the time spent running up to it,
and the rush telegraphs it.

**Enhanced**, force builds 35% faster and the crash is wider (radius 2.5).

## Look

0.4.0 added these on top of the game's own earth effects; 0.4.1 plays the **game's own earth
sprites** for them wherever they fit, and draws the rest in the game's colours with no outlines:

- **Raise**: the game's floor crack splits the ground under the slab.
- **The shield fills with force** (drawn): glowing cracks spread through the game's shield sprite
  itself (cut to its actual outline, read from the sprite), radiating from a point of strain at its
  middle, with the light running outward along them (4 frames, looping). More and longer cracks at
  each 25%, in the game's fire tones: orange at 25%, yellow at 50%, pale yellow-white from 75%; at
  full an earth rune (an open diamond round a cross) burns in the middle, pulsing faster.
- **Each 25% of force**: the game's dust rings out over the floor, wider each step.
- **Running**: dust thrown back from your feet; from half force the game's floor cracks open behind
  you (`FloorCrackEffects` sheet), as well as its floor spikes. The game's debris rocks (`RockMed`
  sheet) circle you, tumbling by quarter turns: two at 50%, three at 75%, four at full, warmed then
  and shedding embers (drawn in the game's fire tones), which also rise off you and the cracks.
- **Unload**: the circling rocks are flung ahead along the rush, cracks tear where you push off
  and halfway, and dust blasts back.
- **Crash**: the game's crater (the large one from half force) and its floor crack spread to the
  crash's size, a ring of the game's rock spikes (`EarthWave` sheet) erupting at the crash's edge
  one after another round from the front (5 to 8), and 4 to 10 of its stones (`Rocks_N`) flung in
  arcs, mostly ahead. Shake and hit-stop are unchanged.

In a level that hasn't loaded those sheets, drawn frames stand in (rocks, cracks and a crater in
the game's rock browns, sampled from its debris sheets), no outlines, fading out by alpha; the
spike ring is then left out.

Set `[Look] DrawnEffects = false` for the 0.3.0 look. Everything here is cosmetic: damage, hit
areas, timings and movement are unchanged.

## Log

`BepInEx\LogOutput.log` shows:

- `Stone Charge 0.4.1 registered` at start-up.
- The look, once per visual, which art it uses: `Stone Charge: visual 'stone chunk' uses the game's
  RockMed sheet (4 rocks in 32x32 cells) (4 sprites of 32x32, ppu 16)` (also `'rock shard'`,
  `'floor crack'`, `'crater'`, `'rock spikes'`), or `visual '<name>': the game's ... isn't loaded
  here, using the drawn frames` followed by `visual '<name>' ready (N sprites of WxH, ppu 16)` when
  it draws its own; `visual 'ember' ready (...)`; and `visual 'shield cracks' ready for <sprite>
  (16 sprites of WxH, ppu P, N crack pixels, rune, cut to the shield's outline (M px))` once per
  shield facing (`an ellipse in its bounds (couldn't read the sprite)` if the sprite couldn't be
  read, `no room for the rune` if it's too small).
- `Stone Charge: drawn: shield cracks at stage 4 of 4, 9 floor crack(s), 20 ember(s), 4 chunk(s)
  flung, 10 shard(s), 8 spike(s); N fx sprite(s) live` as each cast ends.
- `Stone Charge: cast: shield up for 4 s, force builds 25%/s at base run speed (full in 4.0 s at
  x1.00, 2.9 s sprinting); press again to crash, 30% less from hits in front, braced` each cast
  (`cast (enhanced)` when it is).
- `Stone Charge: run speed 6.5 of base 6.5 (x1.00; ...)` once, the first speed read.
- `Stone Charge: sprinting (+40% run speed) after 1.0 s on the move; run speed now 9.1 u/s` when
  the sprint kicks in.
- `Stone Charge: force 25% after 1.0 s (speed x1.00)`, then 50%, 75% and 100%, each with the
  speed read at the time (`, sprinting` / `, enhanced` when so).
- `Stone Charge: blocked a hit from the front with the shield (30% less damage)` the first time
  the armour applies in a cast.
- `Stone Charge: unload (press) at 100% force after 3.1 s (speed x1.40, peak x1.40): rush 2.5 u,
  bash 12 + crash 24 damage (x6.0), knockback 70, stun x2.5, crash radius 2.0` on the second press
  (`unload (ran into <enemy>)` when the shield hit an enemy, `unload (dashed into <enemy>)` when it
  hit one during a dash or its slide, `unload (timeout)` when the time ran out), with `; took N
  hit(s) on the shield, M from the sides or behind` if you were hit.
- `Stone Charge: dash 1 behind the shield: toward (0.71, 0.71) at S u/s for 0.125 s (~D u,
  Dash's dash), 45% force kept, 2.2 s of shield left` each dash while the shield is up (S and D
  are your dash speed stat and the distance it gives; the name is your dash arcana's ID)
  (`(chained, slide skipped)` with the skip-the-slide relic, `, evading` with a dash-evade relic),
  then one of:
  - `Stone Charge: dash 1 over without a contact crash: 45% force kept, 1.8 s of shield left` once
    its slide is over;
  - `Stone Charge: dash 1 ended in a contact crash: <enemy> at 45% force` (followed by the
    `unload (dashed into <enemy>)` line) when the shield hit an enemy during the dash or slide;
  - `Stone Charge: dash 1 cut short by the unload (press)` (or `(timeout)`).
- `Stone Charge: crash: bash hit 1, crash hit 2 for 24 damage each before armour (EarthArcher(Clone),
  ...)` once the rush is over, or `crash hit nothing (...)`.
- `Stone Charge: shield dropped (died) after 1.8 s with 45% force lost` when a death (or a fall,
  `(fell)`, landing a dash on a pit) cuts it short; `cancelled (dashed) before the shield went up`
  when you dash out of the slam.
- `Stone Charge: the shield turns with you` once; `couldn't ...` / `effect '...' unavailable` lines
  if a game object or a cosmetic effect can't be made (the arcana carries on without it).

## Testing it

At the house dummies: cast it facing a dummy (the shove hits for 3), run in circles for 3-4 seconds
watching the `force` lines come in (debris at 25%, floor spikes from 50%), then run at the dummy
and press the button again: you rush 2.5 units and the dummy takes the bash and the crash, shown in
the `unload` and `crash:` lines. Cast and press again straight away (standing still) for the weak
version: `unload (press) at 0% force ... bash 2 + crash 4 damage (x1.0), knockback 10`. Dash while
charging: the shield stays up and turns with you, the `force` you had is kept (`dash 1 behind the
shield ... force kept`, then `dash 1 over without a contact crash`); run up some force, then dash
straight at the dummy for `dash N ended in a contact crash` and `unload (dashed into ...)`.

## Settings

`BepInEx\config\mdbailey94.wol.stonecharge.cfg`:

| Section | Setting | Default | What it does |
| --- | --- | --- | --- |
| Charge | `ChargeTime` | 4 | Seconds the shield stays up before it crashes by itself |
| Charge | `MinPressTime` | 0.2 | Seconds after the cast before a second press crashes |
| Charge | `EnergyPerSecond` | 25 | % of full force stored per second at base run speed |
| Charge | `EnhancedEnergyBonus` | 35 | % faster the force builds when enhanced |
| Charge | `SprintDelay` | 1 | Seconds on the move before the sprint (+40%) kicks in |
| Charge | `FrontArmour` | 30 | % less damage from hits in front (0 = off) |
| Charge | `Brace` | true | No stagger or knockback while the shield is up |
| Crash | `RushDistance` / `RushTime` | 2.5 / 0.12 | The rush |
| Crash | `BashRadius` | 1.2 | Hit area of the bash during the rush |
| Crash | `CrashRadius` / `EnhancedCrashRadius` | 2 / 2.5 | Hit area of the crash |
| Crash | `FullMultiplier` | 6 | Bash and crash damage at full force, as a multiple of the base |
| Crash | `Knockback` / `FullKnockback` | 10 / 70 | Bash and crash knockback at no / full force |
| Crash | `StunEnergy` / `StunMultiplier` | 75 / 2.5 | From this % force the crash's hitstun is this much longer |
| Balance | `RaiseDamage` / `RaiseKnockback` | 3 / 8 | The shove as the shield forms |
| Balance | `BashDamage` / `CrashDamage` | 2 / 4 | Base damage of the bash and the crash |
| Balance | `Cooldown` | 6 | Cooldown in seconds |
| Look | `DrawnEffects` | true | The shield cracks and rune, circling rocks, cracks, crater, spikes and stones (off = 0.3.0 look) |

**Charge** and **Crash** settings take effect on the next cast; **Balance** settings after
restarting the game.

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLStoneCharge.zip`
from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it
into your Wizard of Legend folder. It shows up in the arcana shop (or straight away with Unlock Mod
Content on).

## Building it yourself

Put `Assembly-CSharp.dll`, `UnityEngine.dll` (from `WizardOfLegend_Data\Managed`), `BepInEx.dll`
(from `BepInEx\core`) and `LegendApi.dll` into a `lib` folder at the repo root, then run
`dotnet build -c Release`.
