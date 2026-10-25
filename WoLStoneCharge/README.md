# Stone Charge (Wizard of Legend)

A new **Earth standard arcana**, added with [LegendAPI](https://github.com/yekoc/LegendAPI).

> **Stone Charge**: Raise a stone bulwark and keep running: the faster you move, the more force it
> stores. Run into a foe or press again to crash forward and unload all of it!
>
> *Enhanced: Stores force faster and crashes wider!*

| Stat | Value |
|------|-------|
| Element | Earth |
| Tier | 4 (like Aegis Charge and Terra Ring) |
| Price | 6 (like Aegis Charge and Terra Ring) |
| Damage | Shove 3 as the shield forms. Bash 2 and crash 4 at no stored force, scaling evenly to 12 and 24 at full (6x): an enemy caught by both takes 6 to 36 |
| Knockback | Bash and crash 10 at no force to 70 at full, along the way you rush; the crash's hitstun is 2.5x from 75% force up |
| Shield up | up to 4 s; 30% less damage from hits in front, no stagger (you still take the damage) |
| Force | 25% a second running at base speed, more the faster you are (x1.4 sprinting, x1.15 a speed relic...), nothing standing still. Enhanced +35% |
| Rush | 2.5 units in 0.12 s; crash radius 2 (enhanced 2.5) a unit ahead of where you stop |
| Cooldown | 6 s, counting from the crash (or a cancel) |

**Cast** it and your wizard slams the ground and a slab of stone (the game's own Aegis Charge
shield) rises a unit in front of them, shoving back whatever's standing there. Then, for up to
**4 seconds**, you run about freely behind it (it turns to face the way you run) and **every moment
on the move stores force** in it, in proportion to your run speed against its base: running at base
speed stores 25% a second (full in 4 s), a sprint (+40%, which you work up to after a second on the
move, since the game drops its own sprint when you cast) stores it in about 2.9 s, and speed relics,
the cloak and speed buffs count too. Standing still stores nothing. Rock debris gathers round you as
it builds, the ground cracks behind you from half force, and a rock aura swirls at full.

Behind the shield, hits coming from in front do **30% less** and nothing staggers or knocks you
back, but you still take the damage, you can't cast anything else, and a **dash drops the shield
and loses all the force**.

**Run the shield into an enemy** or **press the button again** (or let the 4 seconds run out) to
**unload** with whatever force is stored by then (0.2.0: running into an enemy crashes at once,
anything within 0.9 of the shield's spot while you're on the move): a short rush forward of
2.5 units, bashing with the shield on the way (level 2) and crashing where it ends (level 3), both
shoving enemies along your line. Their damage and knockback scale with the stored force: an instant
crash is weak (2 + 4 damage, knockback 10), a crash after a long sprint is not (12 + 24, knockback
70, and a longer stun from 75% force up). So the big hit costs you the time spent running up to it,
and the rush telegraphs it.

**Enhanced**, force builds 35% faster and the crash is wider (radius 2.5).

## Log

`BepInEx\LogOutput.log` shows:

- `Stone Charge 0.2.0 registered` at start-up.
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
  (`unload (ran into <enemy>)` when the shield hit an enemy, `unload (timeout)` when the time ran
  out), with `; took N hit(s) on the shield, M from the sides
  or behind` if you were hit.
- `Stone Charge: crash: bash hit 1, crash hit 2 for 24 damage each before armour (EarthArcher(Clone),
  ...)` once the rush is over, or `crash hit nothing (...)`.
- `Stone Charge: shield dropped (dashed) after 1.8 s with 45% force lost` when a dash (or a death)
  cuts it short.
- `Stone Charge: the shield turns with you` once; `couldn't ...` / `effect '...' unavailable` lines
  if a game object or a cosmetic effect can't be made (the arcana carries on without it).

## Testing it

At the house dummies: cast it facing a dummy (the shove hits for 3), run in circles for 3-4 seconds
watching the `force` lines come in (debris at 25%, floor spikes from 50%), then run at the dummy
and press the button again: you rush 2.5 units and the dummy takes the bash and the crash, shown in
the `unload` and `crash:` lines. Cast and press again straight away (standing still) for the weak
version: `unload (press) at 0% force ... bash 2 + crash 4 damage (x1.0), knockback 10`. Dash while
charging for the `shield dropped (dashed)` line.

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
