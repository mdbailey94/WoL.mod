# Thunderhead (Wizard of Legend)

A new **Lightning standard arcana**, added with [LegendAPI](https://github.com/yekoc/LegendAPI). It
has a **signature version**, so it can go in the signature slot.

> **Thunderhead**: Leap high into the air, out of reach, and hover as lightning rains down around
> you, then crash down in a thunderous slam!
>
> *Enhanced: Stay up longer, strike wider and slam harder!*
>
> *Signature: A true thunderstorm, twice as long with lightning raining twice as fast over a wider
> area, ending in a huge shocking slam!*

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

## Signature

Put Thunderhead in the **signature slot** (the spellbook in your house lists it under Signature
Arcana) and cast it with the signature gauge full: the leap becomes a **true thunderstorm**. The
wizard hangs in the air **twice as long** (2 s; 3 s when the arcana is also enhanced, which it
always is in the signature slot), lightning rains at **twice the rate** (volleys of three every
eighth of a second, 8 damage each) over a circle **half again as wide**, under a bigger, flickering
storm cloud, and the landing is a **bigger slam** (half again the size, 35 damage) that **always
shocks**, with a bigger bolt, more dust and a harder shake. Hover speed is the same as ever. All of
it is multipliers on the normal (or enhanced) values, in the config's `[Signature]` section. As with
the game's own arcana the cast spends the whole gauge and its hits don't refill it.

The signature version is **unlocked along with the arcana** (buy it once and it's in the Signature
list). If you'd rather earn it the vanilla way, finding its Signature drop in a run, turn
`SignatureUnlocksWithArcana` off.

The bolts are the game's own lightning from the sky (just its animation; the hits come from the
game's lightning bursts). The log says `Thunderhead: using the game's lightning bolts`. Each cast
also logs a `Thunderhead: cast (normal), ...` line (the version that ran: `normal`, `enhanced`,
`signature` or `both: enhanced + signature`, then air time, volley rate and radius, slam size and
hover speed) and a `Thunderhead: landed ...` line (`Thunderhead: signature storm landed ...` for a
signature cast: how far you moved, how many strikes were fired and how many times they and the
slam hit). When the signature is unlocked you get `Thunderhead: signature unlocked along with the
arcana ...` once. The gauge fills by dealing damage; the training dummies in your house work for that.

## Settings

Everything is adjustable in `BepInEx\config\mdbailey94.wol.thunderhead.cfg` (made the first time
you start the game with the mod), or in-game with a config manager mod if you have one.

| Section | Setting | Default | What it does |
| --- | --- | --- | --- |
| General | `SignatureUnlocksWithArcana` | true | Unlock the signature version along with the arcana (off: find its Signature drop in a run) |
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
| Signature | `AirTimeMultiplier` | 2 | The signature cast hangs up this many times as long |
| Signature | `StrikeRateMultiplier` | 2 | Volleys come this many times as often |
| Signature | `RadiusMultiplier` | 1.5 | The strikes' circle is this many times as wide |
| Signature | `SlamSizeMultiplier` | 1.5 | The signature slam's hit area, times the slam size |
| Look | `Cloud` / `CloudSize` | true / 1 | Ride a dark storm cloud while up, and its size (the signature cloud is 1.4x) |
| Look | `Electric` | true | Crackle with electricity while in the air |
| Look | `AuraSize` | 1 | Size of the electric aura in the air |
| Balance | `StrikeDamage` / `SlamDamage` | 5 / 14 | Damage |
| Balance | `SignatureStrikeDamage` / `SignatureSlamDamage` | 8 / 35 | Damage in the signature cast (its slam always shocks) |
| Balance | `StrikeKnockback` / `SlamKnockback` | 6 / 45 | Knockback |
| Balance | `StrikeShockChance` / `SlamShockChance` | 0.2 / 0.5 | Chance to shock (0 to 1) |
| Balance | `Cooldown` | 7 | Cooldown in seconds |

**Leap**, **Lightning**, **Signature** and **Look** settings take effect the next time you cast it;
**Balance** settings after restarting the game. (0.6.0 replaced `AirMoveSpeed` with
`HoverSpeedPercent` and raised the cooldown from 6 to 7; an old `AirMoveSpeed` line left in the
config file is ignored. 0.7.0 added the signature version.)

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLThunderhead.zip` from
the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it into
your Wizard of Legend folder. It shows up in the arcana shop (or straight away with Unlock Mod
Content on).
