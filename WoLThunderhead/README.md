# Thunderhead (Wizard of Legend)

A new **Lightning standard arcana**, added with [LegendAPI](https://github.com/yekoc/LegendAPI).

> **Thunderhead**: Leap high into the air, out of reach, and hover as lightning rains down around
> you, then crash down in a thunderous slam!
>
> *Enhanced: Stay up longer, strike wider and slam harder!*

A leap like Heroic Leap's:

- A **very short crouch** on the ground (about a tenth of a second, when you can still be hit),
  then your wizard **leaps high, straight up**. Up there they **can't be hit**, the way the game's
  jumping arcana work: their hurtbox is switched off (attacks and projectiles pass through) and
  they count as airborne. Their shadow stays on the ground below, they **ride a dark storm
  cloud**, and they **crackle with electricity** (the game's electric aura, sparks and a
  flickering blue-yellow glow). They stay drawn in front of statues and pillars they were in
  front of on the ground.
- They **hover** up there and can **steer slowly with the stick**, at 40% of their move speed
  (speed relics count), facing the way they go; the lightning and the landing follow them. Walls
  stop them as usual, and so does the edge of a pit (they won't drift out over one and drop in
  when they land, unless they can walk on air anyway).
- While up (1 second), **lightning crashes down** around the spot below in volleys of three every
  quarter second (5 damage each, can shock), spread evenly: over the leap they cover the whole
  area without bunching up, each volley striking near, middle and far all round you.
- Then they **crash back down** in a **lightning slam**: 14 damage, a big knockback and a likely
  shock, with a camera shake and a short hit-stop. About 1.4 seconds in the air in all.
- Dash to cancel only before take-off. Cooldown 7 seconds.
- **Enhanced**: 1.5 seconds up, strikes over a wider area and a bigger slam.

The bolts are the game's own lightning from the sky (just its animation; the hits come from the
game's lightning bursts). The log says `Thunderhead: using the game's lightning bolts`. Each cast
also logs a `Thunderhead: cast ...` line (air time and hover speed) and a `Thunderhead: landed ...`
line (how far you moved, how many strikes were fired and how many times they and the slam hit).

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
| Leap | `HoverSpeedPercent` | 40 | How fast you steer the hover, as a percentage of your move speed (0 = hang still) |
| Leap | `RecoverTime` | 0.15 | Seconds on the ground after the slam |
| Lightning | `Radius` / `EnhancedRadius` | 2.2 / 2.8 | How far from you the strikes land |
| Lightning | `VolleyInterval` / `EnhancedVolleyInterval` | 0.25 / 0.25 | Seconds between volleys (lower = more often) |
| Lightning | `VolleySize` | 3 | Strikes per volley |
| Lightning | `EvenSpread` | true | Spread the strikes evenly over the area (off: random spots) |
| Lightning | `StrikeSize` | 1 | Each strike's hit area |
| Lightning | `SlamSize` / `EnhancedSlamSize` | 2.4 / 3 | The landing slam's hit area |
| Look | `Cloud` / `CloudSize` | true / 1 | Ride a dark storm cloud while up, and its size |
| Look | `Electric` | true | Crackle with electricity while in the air |
| Look | `AuraSize` | 1 | Size of the electric aura in the air |
| Balance | `StrikeDamage` / `SlamDamage` | 5 / 14 | Damage |
| Balance | `StrikeKnockback` / `SlamKnockback` | 6 / 45 | Knockback |
| Balance | `StrikeShockChance` / `SlamShockChance` | 0.2 / 0.5 | Chance to shock (0 to 1) |
| Balance | `Cooldown` | 7 | Cooldown in seconds |

**Leap**, **Lightning** and **Look** settings take effect the next time you cast it; **Balance** settings
after restarting the game. (0.6.0 replaced `AirMoveSpeed` with `HoverSpeedPercent` and raised the
cooldown from 6 to 7; an old `AirMoveSpeed` line left in the config file is ignored.)

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLThunderhead.zip` from
the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it into
your Wizard of Legend folder. It shows up in the arcana shop (or straight away with Unlock Mod
Content on).
