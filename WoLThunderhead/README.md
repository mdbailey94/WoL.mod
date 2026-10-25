# Thunderhead (Wizard of Legend)

A new **Lightning standard arcana**, added with [LegendAPI](https://github.com/yekoc/LegendAPI). It
has a **signature version**, so it can go in the signature slot.

> **Thunderhead**: Leap high into the air, out of reach, as lightning rains down around you, then
> crash down in a thunderous slam!
>
> *Enhanced: Steer through the air as lightning strikes faster! Signature: a raging storm and a
> devastating dive!*

A leap like Heroic Leap's:

- A **very short crouch** on the ground (about a tenth of a second, when you can still be hit),
  then your wizard **leaps high, straight up**. Up there they **can't be hit**, the way the game's
  jumping arcana work: their hurtbox is switched off (attacks and projectiles pass through) and
  they count as airborne. Their shadow stays on the ground below, they **ride a dark storm
  cloud**, and they **crackle with electricity** (the game's electric aura, sparks and a
  flickering blue-yellow glow). They stay drawn in front of statues and pillars they were in
  front of on the ground.
- They **hang in place** over the spot they leapt from for 1 second while **lightning crashes
  down** around it: volleys of two every 0.4 seconds (5 damage each, can shock), spread evenly
  over the area, each volley striking near and far.
- Then they **crash back down** in a **lightning slam**: 14 damage, a big knockback and a likely
  shock, with a thunderclap, a camera shake and a short hit-stop. About 1.4 seconds in the air in all.
- Dash to cancel only before take-off. Cooldown 7 seconds.

## Enhanced

- They can **steer the hover slowly with the stick**, at 40% of their move speed (speed relics
  count), facing the way they go; the lightning and the landing follow them. Walls stop them as
  usual, and so does the edge of a pit (they won't drift out over one and drop in when they land,
  unless they can walk on air anyway).
- Lightning strikes **more often**: volleys of three every quarter second.
- **1.5 seconds** up, strikes over a **wider area** and a **bigger slam**.

## Signature

Put Thunderhead in the **signature slot** (the spellbook in your house lists it under Signature
Arcana) and cast it with the signature gauge full. It builds on the enhanced leap (the game always
enhances the signature slot) and turns it into a **cinematic storm**:

1. **Take-off**: the wizard launches on a bolt from the sky, with a flash, a thunderclap and a
   shake, and the stage starts going **dark**.
2. **The storm** (2.4 s, steerable): lightning rains down **every tenth of a second** (8 damage
   each) over a circle half again as wide, each strike with sparks and a scorch mark and the
   camera jolting, under a storm cloud that **swells** as it rages.
3. **Wind-up**: at the top the wizard holds for a quarter second, rising a little and crackling
   with sparks, as the camera **pushes in** and the stage goes darker still.
4. **The dive**: a streak of lightning straight down and a fast dive.
5. **Impact**: the game **freezes for 4 frames** under a **white flash**, a huge shocking slam
   (35 damage, half again the enhanced size) with a crater, rays of light and a **big shake**; a
   **ring of 8 lightning bolts** radiates out to strike just beyond the slam (8 damage each), the
   **light floods back** and time eases back from a moment of **slow motion** as the camera
   pulls out.

About 3 seconds in the air in all. Every camera, light and time change is put back afterwards,
however the cast ends (landing, being cut short, death or a level load). As with the game's own
arcana the cast spends the whole gauge and its hits don't refill it. Turn `Cinematic` off for the
storm without the camera, light and time effects.

The signature version is **unlocked along with the arcana** (buy it once and it's in the Signature
list). If you'd rather earn it the vanilla way, finding its Signature drop in a run, turn
`SignatureUnlocksWithArcana` off.

## Log

The bolts are the game's own lightning from the sky (just its animation; the hits come from the
game's lightning bursts). The log (`BepInEx\LogOutput.log`) says:

- `Thunderhead: cast (standard), ...`: the version that ran (`standard`, `enhanced`, `signature`
  or `both: enhanced + signature`), then the air time, volley size and rate, radius, slam size and
  `steering off (hangs still)` or `steering on at 40% of move speed ...` (and `cinematic on/off`
  for a signature cast).
- `Thunderhead: landed ...` (`Thunderhead: signature storm landed ...`): how far you moved, how many
  strikes were fired and how many times they, the slam and the ring hit.
- For a signature cast, a line for each beat: `Thunderhead: signature take-off: ...`,
  `Thunderhead: signature wind-up ...`, `Thunderhead: signature dive ...`,
  `Thunderhead: signature impact: hit-stop 4 frames, flash 0.85, stage light ...`,
  `Thunderhead: signature ring: 8 bolts ...` and, once it has all settled,
  `Thunderhead: signature cinematic restored (landed): stage light ..., camera size ..., time scale 1.00`.
- When the signature is unlocked you get `Thunderhead: signature unlocked along with the arcana ...`
  once. The gauge fills by dealing damage; the training dummies in your house work for that.

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
| Leap | `CrashTime` | 0.12 | Seconds crashing down (the signature dive takes 0.6x) |
| Leap | `HoverSpeedPercent` | 40 | Enhanced and signature: how fast you steer, as a percentage of your move speed |
| Leap | `RecoverTime` | 0.15 | Seconds on the ground after the slam |
| Lightning | `Radius` / `EnhancedRadius` | 2.2 / 2.8 | How far from you the strikes land |
| Lightning | `VolleyInterval` / `EnhancedVolleyInterval` | 0.4 / 0.25 | Seconds between volleys (lower = more often) |
| Lightning | `VolleySize` / `EnhancedVolleySize` | 2 / 3 | Strikes per volley |
| Lightning | `EvenSpread` | true | Spread the strikes evenly over the area (off: random spots) |
| Lightning | `StrikeSize` | 1 | Each strike's hit area |
| Lightning | `SlamSize` / `EnhancedSlamSize` | 2.4 / 3 | The landing slam's hit area |
| Signature | `AirTimeMultiplier` | 1.6 | The storm lasts this many times the enhanced air time |
| Signature | `StrikeRateMultiplier` | 2.5 | Volleys come this many times as often as enhanced |
| Signature | `RadiusMultiplier` | 1.5 | The strikes' circle is this many times as wide |
| Signature | `SlamSizeMultiplier` | 1.5 | The signature slam's hit area, times the enhanced slam size |
| Signature | `WindupHold` | 0.25 | Seconds holding at the top before the dive |
| Signature | `RingBolts` | 8 | Bolts in the ring round the landing (0 = none) |
| Signature | `Cinematic` | true | The camera, light and time effects below |
| Signature | `StageDarken` | 0.45 | How much of the stage light is left during the storm (1 = no darkening) |
| Signature | `CameraZoom` | 0.9 | Camera size during the wind-up and dive (1 = no zoom) |
| Signature | `HitStopFrames` | 4 | Frames frozen at the impact |
| Signature | `ImpactFlash` | 0.85 | Strength of the white flash at the impact (0 = none) |
| Signature | `ImpactSlowMo` | 0.3 | Seconds of slow motion after the hit-stop (0 = none) |
| Look | `Cloud` / `CloudSize` | true / 1 | Ride a dark storm cloud while up, and its size (the signature cloud is 1.4x and swells) |
| Look | `Electric` | true | Crackle with electricity while in the air |
| Look | `AuraSize` | 1 | Size of the electric aura in the air |
| Balance | `StrikeDamage` / `SlamDamage` | 5 / 14 | Damage |
| Balance | `SignatureStrikeDamage` / `SignatureSlamDamage` | 8 / 35 | Damage in the signature cast (strikes and ring; its slam always shocks) |
| Balance | `StrikeKnockback` / `SlamKnockback` | 6 / 45 | Knockback |
| Balance | `StrikeShockChance` / `SlamShockChance` | 0.2 / 0.5 | Chance to shock (0 to 1) |
| Balance | `Cooldown` | 7 | Cooldown in seconds |

**Leap**, **Lightning**, **Signature** and **Look** settings take effect the next time you cast it;
**Balance** settings after restarting the game.

**0.8.0** rebalanced the tiers: the standard leap hangs still with fewer strikes (volleys of 2
every 0.4 s, was 3 every 0.25 s with steering), steering and the faster strikes are the enhanced
version's, and the signature storm is shorter and fiercer (1.6x the air time at 2.5x the rate, was
2x and 2x) with the cinematic dive. A config file from an earlier version that still has the old
defaults for those settings is moved to the new ones the first time 0.8.0 starts (the log says
which); values you changed yourself are kept. (0.6.0 replaced `AirMoveSpeed` with
`HoverSpeedPercent` and raised the cooldown from 6 to 7; an old `AirMoveSpeed` line left in the
config file is ignored. 0.7.0 added the signature version.)

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLThunderhead.zip` from
the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it into
your Wizard of Legend folder. It shows up in the arcana shop (or straight away with Unlock Mod
Content on).
