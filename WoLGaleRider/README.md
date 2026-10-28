# Gale Rider (Wizard of Legend)

A new **Air standard arcana**, added with [LegendAPI](https://github.com/yekoc/LegendAPI).

> **Gale Rider**: Summon a ball of wind and ride it! Steer with the stick at a boosted pace while
> the ball rolls over enemies, shoving them away in the direction you're heading. The faster you
> go, the bigger the ball and the harder the shove!
>
> *Enhanced: Ride longer and the ball grows even larger!*

A ride like Aero Ring's, with Tearing Whirlwind's rolling hit:

- Cast it and a **ball of wind** forms under you. For **3 seconds** you **ride it**: steer with
  the stick, at your move speed **+40%** (a multiplier on the stat, so speed relics count; 40%
  matches a sprint, so you're never slower on the ball than running). Let the stick go and the
  ball rolls on the way it was going. Walls stop you as usual, and so does the edge of a pit
  (you won't roll out over one, unless you can walk on air anyway).
- The ball is a **rolling attack round you**: an enemy it rolls over takes **4 damage** and is
  **shoved away in the direction you're heading**, at most about **three times a second** per
  enemy. You roll through enemies rather than bumping into them.
- The ball's **size and shove grow with your speed**: your move-speed stat against its base
  (the ride bonus, speed relics, the cloak and speed-boost arcana all count; slows shrink it; the
  speed a dash forces for a moment is ignored). Running at base speed (x1.0) is the smallest ball
  (hit radius 1.2) and a moderate shove (knockback 15, Aero Ring is 25); **x2.0 base speed** is
  the biggest (radius 2.4) and a very strong one (knockback 70, Rushing Typhoon's final shove),
  evenly between. Riding alone puts you at x1.4; with two speed relics about x1.85.
- **Dash** to hop off at any time, or **press the button again** (after 0.4 s). The ride ends in
  the game's slide, like Tearing Whirlwind. Cooldown 7 seconds (from the cast).
- **Enhanced**: 4.5 seconds on the ball, and the biggest ball is radius 3.

## 0.4.1

- The signature storm ball **carries enemies with it** (Matt): every enemy it hits, up to 8
  (bosses and the floor boss excepted), is swept along inside it, through the leap, until the
  slam lands on them. One a wall catches behind is let go. The slam's log line says how many it
  carried.

## 0.4.0: a cinematic signature (asked for by Matt)

Gale Rider has a **signature version** now (the spellbook's Signature page; unlocked along with
the arcana, `[General] SignatureUnlocksWithArcana`):

1. **Storm ball**: the enhanced ball made **twice the size** (hit and look; `[Signature]
   BallSize`), hitting for 8 (`[Balance] SignatureDamage`) with half again the shove, ridden for
   **6 s** (`RideTime`). The camera pulls back 20% and the room darkens as you set off.
2. **Leap**: when the time is up, or you press the button again, the ball comes apart under
   you and you spring **3 units up** (`LeapHeight`), hanging at the top in **slow motion**.
3. **Slam**: you come down in a wind blast round where you land that throws everything outward
   for 40 (`SignatureSlamDamage`), with the game's shockwave, a spinning burst and a ring of
   dust, a 5-frame hit-stop, a white flash, a heavy shake and a moment of near-frozen time.
   Then the camera and light ease back.

A dash still cuts the ride short, with no slam. The camera and light always go back, also
when the cast is cut short and in any case after 9 s (`Gale Rider: signature restored (...)`).

## 0.3.8

- **Facing the way you ride**: the ride set the wizard's facing without the game's left/right
  flip, so riding left showed the right-facing pose. It now turns with the ball.
- **GSlam is the default pose** (`[Look] RidePose`). The game draws its ground slam facing up
  or down only: riding sideways uses a `GroundSlamRight` animation if the wizard has one
  (logged once), otherwise the down pose, flipped to the way you ride.
- **A quarter smaller ball** (picture and hit together): hit radius 0.6 at base speed to 1.2
  at the peak, 1.5 enhanced (`[Ball]`, was 0.8 / 1.6 / 2).
- **Config edits apply while the game runs**: the file is read again within 2 s of being saved
  (logged as "settings read again"). `[Ride]`, `[Ball]` and `[Look]` apply on the next cast,
  `[Balance]` still after a restart. Before, an edit made mid-session never applied, and the
  game could save the old value back over it.

## 0.3.7

- The ball's picture always matches its hit area: `[Look] VortexSize` scales only the swirl
  round it (at 0.3 it had shrunk the picture to a third of the hit).
- `[Ball]` sizes still at the pre-0.3.5 defaults in an older config file are moved to the new
  defaults once (a config file keeps its stored values, so 0.3.5's smaller ball never applied
  on an existing install); sizes you set yourself are left alone.

## 0.3.6

- The ball is the **hand-drawn wind ball** made for the mod (`Assets/galerider_ball*.png`,
  8 frames of 48x48: the whole ball, its far-side strokes drawn behind the wizard and its
  near-side strokes in front). `[Look] Ball` = Drawn (default), Game (the game's WindBall
  sprite, as 0.2.1–0.3.5) or Coded.

## 0.3.5

- The ball is a third smaller (hit radius 0.8 at base speed to 1.6 at the peak, 2 enhanced;
  `[Ball]`); the drawn ball is sized from the hit radius, so picture and hit shrink together.
- `[Balance] HittableWhileRiding` (on): the ride never made you invulnerable itself, but any
  invulnerability the game puts on a riding wizard is cleared while it's on, logged once.

## Look

0.2.0 added these on top of the game's own wind; 0.2.1 plays the **game's own wind sprites** for
them, so they match its art exactly:

- **Cast**: the game's whirlwind forming at your feet over the wind-up (`WindEffects_0-5`), with
  its spinning air burst, a shockwave and a wind trail out the way you set off.
- **The wind ball**: the game's ball of wind (`WindSlash/WindBall0-5`). From 0.3.0 you **sit on
  it**: the ball rolls on the ground under you and your picture rides it, turning with the ball,
  while your shadow stays on the ground (`[Look] RideOnTop`; off, you ride inside it as before,
  behind you with its lower half see-through in front). From 0.3.2 the pose is a **crouch held
  still** (the game's slide, `[Look] RidePose`, at the moment `RidePoseFrame` picks); from 0.3.3
  you crouch **on top** of the ball, feet just into its surface (`RideSeat` 0.9; 1 = right on
  top, 0.4 = legs inside its top, 0 = on the ground), rather than the jump pose. It's sized to the ball's hit area and spins
  faster the faster you go; from speed level 3 a faint copy spins behind it, brighter each level.
  Three thin wind arcs chase round on the floor under it.
- **Travel**: the game's wind wisps (`WindTrail` sheet) flick off the back of the ball, more often
  the faster you go; dust is thrown back from your feet; from level 4 the game's wind trail
  streams behind you. The game's AirVortex swirls and dust ring are still there.
- **Hits**: the game's gust arc breaking up (`WindSlash/WindArrowBreak0-5`), turned to open along
  the shove, with its wind burst. Hit feel (shake, hit-stop) is unchanged.
- **End**: the game's whirlwind lifting and coming apart (`WindEffects_8-12`) with its broken wind
  ring spreading on the floor (`WindParticles` sheet) as you skid off.

In a level that hasn't loaded those sheets, drawn frames stand in, in the game's wind colours
(sampled from its sheets: `#cfebeb`, `#b6d5d5`, `#8b9d9d`), no outlines, fading out by alpha.

Set `[Look] DrawnEffects = false` for the 0.1.0 look. Everything here is cosmetic: damage, hit
area, timings and movement are unchanged. The ride's time shows on the cast bar.

## In the log

Each cast logs:

- `Gale Rider: cast, riding 3.00s, speed +40% (8.0 -> 11.2 u/s)` (the speed before and after
  the ride mod goes on);
- `Gale Rider: speed x1.40 -> level 4 of 10: ball radius 1.68, knockback 37` whenever the speed
  read moves the ball to a new level (at most four times a second);
- `Gale Rider: ride over after 3.00s, travelled 24.3 u, hit 7 time(s), biggest knockback 37
  (last speed x1.40, level 4)` when it ends (`hopped off`, `dashed off` or `cut short` when it
  ended early; `, held back at a pit's edge` if a pit stopped you).

The first cast also logs the level mapping and the raw speed read (`run speed ... of base ...`).

The look logs once per visual which art it uses: `Gale Rider: visual 'wind ball' uses the game's
WindSlash/WindBall0-5 (6 sprites of 48x48, ppu 16)` (also `'gather'`, `'unravel'`, `'gust puff'`,
`'wind streak'`, `'unravel ring'`), or `visual '<name>': the game's ... isn't loaded here, using
the drawn frames` followed by `visual '<name>' ready (N sprites of WxH, ppu 16)` when it draws its
own; and `wind ball drawn on the wizard's layer '...'`. The end-of-ride line then adds `; drawn:
ball 3.0 u wide (the game's WindBall), 25 streak(s), 4 gust puff(s), N fx sprite(s) live`. If any
effect fails it logs `effect '<name>' unavailable, skipping it: ...` once and the ride carries on
without it.

## Settings

Everything is adjustable in `BepInEx\config\mdbailey94.wol.galerider.cfg` (made the first time you
start the game with the mod), or in-game with a config manager mod if you have one.

| Section | Setting | Default | What it does |
| --- | --- | --- | --- |
| Ride | `RideTime` / `EnhancedRideTime` | 3 / 4.5 | Seconds on the ball |
| Ride | `RideSpeedPercent` | 40 | Move-speed bonus while riding (a multiplier on the stat) |
| Ride | `HopOffOnPress` | true | Press the button again to hop off early |
| Ride | `MinRideTime` | 0.4 | Seconds before you can hop off by pressing again |
| Ball | `MinSize` | 1.2 | Hit radius at base run speed |
| Ball | `MaxSize` / `EnhancedMaxSize` | 2.4 / 3 | Hit radius at the peak speed |
| Ball | `PeakSpeedPercent` | 200 | Speed (% of base run speed) at which the ball peaks |
| Look | `VortexSize` | 1 | Size of the swirling wind, relative to the ball |
| Look | `Dust` | true | Kick up dust as the ball rolls |
| Look | `DrawnEffects` | true | The wind ball, streaks, gusts, gather and unravel (off = 0.1.0 look) |
| Balance | `Damage` | 4 | Damage of each hit |
| Balance | `Knockback` / `PeakKnockback` | 15 / 70 | Knockback at base run speed / at the peak |
| Balance | `HitInterval` | 0.33 | Seconds between hits on the same enemy |
| Balance | `Cooldown` | 7 | Cooldown in seconds |

**Ride**, **Ball** and **Look** settings take effect the next time you cast it; **Balance**
settings after restarting the game (`Knockback` and `PeakKnockback` are read on cast too, but the
stat the game keeps needs a restart).

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLGaleRider.zip` from
the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it into
your Wizard of Legend folder. It shows up in the arcana shop (or straight away with Unlock Mod
Content on).

## Building it yourself

Put `Assembly-CSharp.dll`, `UnityEngine.dll` (from `WizardOfLegend_Data\Managed`), `BepInEx.dll`
(from `BepInEx\core`) and `LegendApi.dll` into a `lib` folder at the repo root, then run
`dotnet build -c Release`.
