# Thunderhead (Wizard of Legend)

A new **Lightning standard arcana**, added with [LegendAPI](https://github.com/yekoc/LegendAPI).

> **Thunderhead**: Leap high into the air, out of reach, as lightning rains down around you, then
> crash down in a thunderous slam!
>
> *Enhanced: Stay up longer, strike wider and slam harder!*

A leap like Heroic Leap's:

- A **very short crouch** on the ground (about a tenth of a second, when you can still be hit),
  then your wizard **leaps high, straight up**. Up there they **can't be hit**, the way the game's
  jumping arcana work: their hurtbox is switched off (attacks and projectiles pass through) and
  they count as airborne. Their shadow stays on the ground below.
- While up (1 second), **lightning crashes down** around the spot below in volleys of three every
  quarter second (5 damage each, can shock).
- Then they **crash back down** in a **lightning slam**: 14 damage, a big knockback and a likely
  shock, with a camera shake and a short hit-stop. About 1.4 seconds in the air in all.
- Dash to cancel only before take-off. Cooldown 6 seconds.
- **Enhanced**: 1.5 seconds up, strikes over a wider area and a bigger slam.

The bolts are the game's own lightning from the sky (just its animation; the hits come from the
game's lightning bursts). The log says `Thunderhead: using the game's lightning bolts`.

## Settings

Everything is adjustable in `BepInEx\config\mdbailey94.wol.thunderhead.cfg` (made the first time
you start the game with the mod), or in-game with a config manager mod if you have one.

| Section | Setting | Default | What it does |
| --- | --- | --- | --- |
| Leap | `Height` | 4 | How high the wizard leaps |
| Leap | `WindupTime` | 0.12 | Seconds crouching before take-off (still hittable) |
| Leap | `RiseTime` | 0.25 | Seconds rising to the top |
| Leap | `AirTime` / `EnhancedAirTime` | 1 / 1.5 | Seconds up there while lightning falls |
| Leap | `CrashTime` | 0.12 | Seconds crashing down |
| Leap | `RecoverTime` | 0.15 | Seconds on the ground after the slam |
| Lightning | `Radius` / `EnhancedRadius` | 2.2 / 2.8 | How far from you the strikes land |
| Lightning | `VolleyInterval` | 0.25 | Seconds between volleys |
| Lightning | `VolleySize` | 3 | Strikes per volley |
| Lightning | `StrikeSize` | 1 | Each strike's hit area |
| Lightning | `SlamSize` / `EnhancedSlamSize` | 2.4 / 3 | The landing slam's hit area |
| Balance | `StrikeDamage` / `SlamDamage` | 5 / 14 | Damage |
| Balance | `StrikeKnockback` / `SlamKnockback` | 6 / 45 | Knockback |
| Balance | `StrikeShockChance` / `SlamShockChance` | 0.2 / 0.5 | Chance to shock (0 to 1) |
| Balance | `Cooldown` | 6 | Cooldown in seconds |

**Leap** and **Lightning** settings take effect the next time you cast it; **Balance** settings
after restarting the game.

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLThunderhead.zip` from
the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it into
your Wizard of Legend folder. It shows up in the arcana shop (or straight away with Unlock Mod
Content on).
