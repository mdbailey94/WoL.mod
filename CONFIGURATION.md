# Expanded gameplay configuration

Close the game, back up the .cfg, edit it in a text editor, and relaunch. New keys are written on the first launch with these versions. Existing key values are preserved. All defaults match the previous code; this update does not rebalance abilities. Restart after editing files; live disk reload is not guaranteed across mods.

Base config folder: C:\Program Files (x86)\Steam\steamapps\common\Wizard of Legend\BepInEx\config. Chaos Depths config folder: C:\Users\mdbai\ChaosDepths\profile\BepInEx\config. Existing ability configs are shared by hardlinks on this installation; edit one copy. Ascension and Chaos Depths save/progression files are separate.


## WoLGaleRider 0.5.0

| Section | Key | Default | Range | Meaning |
|---|---|---:|---|---|
| Cinema Advanced | SigDim | 0.65 | 0.1-1 | Stage light multiplier during the signature; 1 leaves brightness unchanged. |
| Cinema Advanced | SigSettleTime | 0.6 | 0.05-3 | Seconds for the signature camera and light to settle. |
| Cinema Advanced | SigZoom | 0.2 | 0-0.7 | Camera pullback fraction; 0 disables this zoom. |
| Cinema Advanced | SlamEaseBack | 0.8 | 0.05-3 | Seconds for camera and light to return after landing. |
| Cinema Advanced | SlamFlash | 1.8 | 0-3 | Landing flash brightness multiplier. |
| Cinema Advanced | SlamHitStopFrames | 5 | 0-15 | Frames of hit-stop at landing; 0 disables it. |
| Cinema Advanced | SlamShake | 2.2 | 0-5 | Landing camera-shake strength; 0 disables it. |
| Cinema Advanced | SlamSlowMoScale | 0.05 | 0.01-1 | Landing time scale; 1 disables slowdown. |
| Cinema Advanced | SlamSlowMoTime | 0.3 | 0-2 | Real seconds of landing slow motion. |
| Cinema Advanced | TopSlowMoScale | 0.15 | 0.01-1 | Time scale at the leap peak; 1 disables slowdown. |
| Cinema Advanced | TopSlowMoTime | 0.35 | 0-2 | Real seconds of slow motion at the top of the leap. |
| Signature Advanced | CarryMargin | 1.2 | 0.1-4 | Extra world units beyond the ball radius before a carried enemy is released. |
| Signature Advanced | LeapCarry | 0.35 | 0-1.5 | Fraction of riding speed retained horizontally during the leap. |
| Signature Advanced | LeapDownTime | 0.2 | 0.05-2 | Seconds to descend during the final leap. |
| Signature Advanced | LeapUpTime | 0.35 | 0.05-2 | Seconds to rise during the final leap. |
| Signature Advanced | SigKnockbackScale | 1.5 | 0-4 | Signature rolling-hit knockback multiplier. |
| Signature Advanced | SlamScale | 4.5 | 0.5-12 | Landing wind-blast size in world units. |

## WoLTrailblazer 0.11.0

| Section | Key | Default | Range | Meaning |
|---|---|---:|---|---|
| Cinema Advanced | CometDim | 0.5 | 0.1-1 | Signature stage-light multiplier; 1 keeps normal brightness. |
| Cinema Advanced | CometSettle | 0.6 | 0.05-3 | Seconds for signature camera and light to settle. |
| Cinema Advanced | CometZoom | 0.15 | 0-0.7 | Camera pullback fraction; 0 disables this zoom. |
| Cinema Advanced | EaseBack | 1 | 0.05-3 | Seconds to restore camera and lighting. |
| Cinema Advanced | FinalFlash | 1.9 | 0-3 | Final blast flash brightness multiplier. |
| Cinema Advanced | FinalHitStopFrames | 5 | 0-15 | Final blast hit-stop frames; 0 disables it. |
| Cinema Advanced | FinalShake | 2.2 | 0-5 | Final blast camera-shake strength; 0 disables it. |
| Cinema Advanced | FinalSlowMoScale | 0.05 | 0.01-1 | Final blast time scale; 1 disables slowdown. |
| Cinema Advanced | FinalSlowMoTime | 0.35 | 0-2 | Real seconds of slow motion after the final blast. |
| Effects Advanced | PillarLife | 0.6 | 0.1-3 | Seconds each eruption fire-column visual remains. |
| Effects Advanced | PillarScale | 2.2 | 0.25-5 | Eruption pillar visual-size multiplier. |
| Scaling Advanced | PeakBurnBonus | 0.5 | 0-1 | Additional burn chance at speed level 10; 0.5 adds 50 percentage points, capped at 100 percent. |
| Scaling Advanced | PeakKnockbackMultiplier | 2.5 | 0-5 | Knockback multiplier at speed level 10; interpolated from 1 at level 1. |
| Signature Advanced | CometFire | 1.6 | 0.25-4 | Signature flame visual-size multiplier. |
| Signature Advanced | CometFuse | 0.8 | 0.05-5 | Game seconds from dropping a signature patch to its eruption. |
| Signature Advanced | CometHitWide | 2 | 0.25-4 | Signature trail hit-width multiplier. |
| Signature Advanced | CometLevels | 2 | 0-8 | Extra fire speed-levels added on top of enhancement; final level is capped at 10. |
| Signature Advanced | CometRing | 1.5 | 0.25-4 | Signature aura hit-size multiplier. |
| Signature Advanced | CometSpeed | 0.25 | 0-1.5 | Added movement speed fraction during the signature; 0.25 means +25 percent. |
| Signature Advanced | FinalDelay | 0.25 | 0.01-2 | Seconds after the last eruption before the final blast. |
| Signature Advanced | FinalRadius | 3 | 0.5-10 | Final blast hit radius in world units. |
| Signature Advanced | PillarStep | 0.04 | 0.01-0.15 | Seconds between remaining patch eruptions when the signature ends. |

## WoLStoneCharge 0.7.0

| Section | Key | Default | Range | Meaning |
|---|---|---:|---|---|
| Cinema Advanced | SigDim | 0.6 | 0.1-1 | Signature stage-light multiplier; 1 keeps normal brightness. |
| Cinema Advanced | SigEaseBack | 1 | 0.05-3 | Seconds for camera and light to return after the slam. |
| Cinema Advanced | SigFlash | 1.6 | 0-3 | Signature slam flash brightness multiplier. |
| Cinema Advanced | SigHitStopFrames | 6 | 0-15 | Signature slam hit-stop frames; 0 disables it. |
| Cinema Advanced | SigShake | 2.6 | 0-5 | Signature slam camera-shake strength; 0 disables it. |
| Cinema Advanced | SigSlowMoScale | 0.05 | 0.01-1 | Signature slam time scale; 1 disables slowdown. |
| Cinema Advanced | SigSlowMoTime | 0.5 | 0-2 | Real seconds of signature slam slow motion. |
| Cinema Advanced | SigZoom | 0.3 | 0-0.7 | Signature camera pullback fraction; 0 disables this zoom. |
| Signature Advanced | ShockwaveAfter | 0.55 | 0.05-2 | Real seconds between signature slam and aftershock. |
| Signature Advanced | ShockwaveScale | 1.6 | 0.25-3 | Aftershock radius as a multiplier of signature slam radius. |
| Signature Advanced | SigArcHeight | 2 | 0-8 | Added signature flight-arc height in world units. |
| Signature Advanced | SigHangHeight | 3.5 | 0-10 | Signature shield hover height in world units. |
| Signature Advanced | SignatureFlightTime | 0.6 | 0.1-2 | Seconds the signature shield flies before hovering. |
| Signature Advanced | SignatureSlamTime | 0.18 | 0.04-1 | Seconds for the signature shield to drop to the ground. |
| Signature Advanced | SigRiseHeight | 6 | 0-15 | Signature shield height at the end of the hover in world units. |
| Throw Advanced | ArcHeight | 1 | 0-5 | Added standard shield flight-arc height in world units. |
| Throw Advanced | HangHeight | 1.6 | 0-6 | Standard shield hover height in world units. |
| Throw Advanced | RiseHeight | 2.6 | 0-10 | Standard shield height at the end of the hover in world units. |
| Throw Advanced | SlamTime | 0.12 | 0.04-1 | Seconds for a standard or enhanced shield to drop to the ground. |
| Throw Advanced | StartHeight | 0.5 | 0-3 | Shield height above its ground position at launch in world units. |

## WoLThunderhead 0.10.0

| Section | Key | Default | Range | Meaning |
|---|---|---:|---|---|
| Aim Advanced | AimedPerVolley | 1 | 0-8 | Number of bolts per standard or enhanced volley that seek enemies; capped by volley size. |
| Aim Advanced | AimFollow | 1.2 | 0-5 | World units a targeted bolt follows an enemy beyond its drawn target marker. |
| Aim Advanced | AimReachPastRadius | 0.5 | 0-5 | Extra world units past the strike circle that can be targeted. |
| Cinema Advanced | RingShake | 0.8 | 0-5 | Camera-shake strength from the lightning ring. |
| Cinema Advanced | SignatureSlamShake | 2.2 | 0-5 | Signature landing camera-shake strength. |
| Cinema Advanced | SlamHitStop | 3 | 0-15 | Standard landing hit-stop frames; 0 disables it. |
| Cinema Advanced | SlamShake | 1.4 | 0-5 | Standard landing camera-shake strength; 0 disables it. |
| Cinema Advanced | StrikeShake | 0.15 | 0-2 | Camera-shake strength for each signature volley. |
| Cinema Advanced | TakeOffShake | 0.6 | 0-5 | Signature take-off camera-shake strength. |
| Effects Advanced | SignatureSlamBoltSize | 2.6 | 0.25-6 | Signature landing lightning-bolt visual size. |
| Effects Advanced | SlamBoltSize | 1.6 | 0.25-5 | Standard landing lightning-bolt visual size. |
| Signature Advanced | RingBurstSize | 1.2 | 0.25-4 | Hit size of each lightning-ring burst in world units. |
| Signature Advanced | RingDelay | 0.06 | 0.01-1 | Seconds after impact before the surrounding lightning ring strikes, capped by [Leap] RecoverTime. |
| Signature Advanced | RingOutside | 0.6 | 0-5 | World units added beyond slam radius for the surrounding lightning ring. |
| Signature Advanced | SignatureDiveSpeed | 0.6 | 0.1-2 | Signature descent duration multiplied by the configured CrashTime; smaller is faster. |
| Signature Advanced | SignatureHoldLift | 0.15 | 0-1 | Additional signature wind-up height as a fraction of configured Height. |

## Existing settings worth adjusting

The following controls already exist; this update preserves their current values.

| Goal | File | Controls |
|---|---|---|
| Level more slowly | mdbailey94.wol.chaosdepths.cfg | [Progression] LevelXpScale (higher needs more XP), XpPerEnemyHealth, BossXpMultiplier, FloorBossXpMultiplier, LevelCap, HealthPerLevel |
| Enemy gear frequency at A1+ | mdbailey94.wol.chaosdepths.cfg | [Loot] FreshDropChance, [Elites] ItemChance, ItemChancePerAscension |
| Boss rewards after first wins | mdbailey94.wol.chaosdepths.cfg | [Loot] BossDropChance, FloorBossDropChance, BossDropMinRarity, FloorBossDropMinRarity |
| Run length | mdbailey94.wol.chaosdepths.cfg | [Dive] FloorsBeforeBoss (0 = normal game floor count), RealmsPerDive (0 = full run) |
| Ascension difficulty | mdbailey94.wol.ascension.cfg | [Level 01] through [Level 10]: EnemyHealth, EnemyDamage, EliteChance, Healing, EnemySpeed, BossHealth, PlayerMaxHealth and other per-level values |
| Ability damage and cooldown | Each mdbailey94.wol.<ability>.cfg | [Balance]; Slingshot Dash uses separate named balance sections for its abilities |
| Other arcana signature size/timing | Cyclone, Rolling Gale, Tidal Wall, Bramble Ring, Slingshot Dash configs | Existing [Signature] or named signature sections |

Fractions: 0.1 means 10% for chances. Multipliers: 1 is unchanged, 1.5 is 50% larger.
Some speed bonuses are added fractions (0.25 = +25%); follow the description for that key.
Rarity indices: 0 Common, 1 Magic/Uncommon, 2 Rare, 3 Legendary with the default loot table.

Fresh-save A0 restrictions from 0.18.0 still apply: no custom elites, rifts or random enemy
gear; fixed Common boss rewards and Magic final reward. A1+ first-win final legendaries
are guaranteed. Legacy drop settings cannot override those guarantees. Signature damage
still cannot generate signature charge. Safety limits, stat-slot IDs and cleanup bounds
remain in code. Shop-card descriptions are not automatically rewritten for custom settings.