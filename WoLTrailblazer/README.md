# Trailblazer (Wizard of Legend)

A new **Fire standard arcana**, added with [LegendAPI](https://github.com/yekoc/LegendAPI).

> **Trailblazer**: Set yourself ablaze! For a few seconds a ring of fire scorches any enemy you
> touch, and running, dashing and movement arcana leave a burning trail behind you. The faster you
> are, the bigger it burns!
>
> *Enhanced: Lasts longer and burns bigger and hotter!*

- **Cast it** and your wizard stamps the ground in a fire burst and is ablaze for **6 seconds**
  (cooldown 12). Dash to cut the stamp short; the fire stays lit.
- While ablaze, their **feet burn** (small flames licking up at their feet), and a **ring of
  fire** round them hits any enemy touching it: **3 damage** at level 1, a **small knockback** and
  a **chance to burn** (50%), moving or not.
- As they move (running, dashing, or carried by any movement arcana) they leave **patches of
  fire** behind them every short distance. Each burns on the ground for about 1.5 seconds and
  hurts enemies who walk into it. It burns with **Searing Rush's fire** (the game's own fire
  columns), small at normal speed and growing to full size as the hits get stronger. The same
  enemy is hit at most about three times a second, ring and trail together.
- **It grows with your speed** over **ten levels**. Running at base speed is **level 1** and
  **2.3x base speed** is **level 10**, with the levels spread evenly between (one level every
  **+14.4%** speed). Speed means your move speed stat with its lasting bonuses: sprinting (the
  game's run speed-up, +40% after two seconds of running), speed relics (+15% each), the cloak
  (Pace +15%, Tempo/Venture/enhanced Hope +10%) and speed-boost arcana (the Air self-buff the
  game calls `AeroRing`: +25% for 2 s, +35% enhanced); the bonuses multiply together. The speeds
  a dash or an attack forces on you for a moment don't count. So:
  - running: level 1, the fire at **half size**; sprinting: **level 3** (x1.40, size 1.1);
  - sprinting in Pace: level 5 (x1.61); with a speed relic too: level 6 (x1.85);
  - sprinting in Pace with a speed relic **and** the speed buff up: **level 10** (x2.31, size
    1.8); two relics get there without the cloak too (x1.85 sprinting, level 6; x2.31 with the
    buff). Two relics, Pace and a sprint are x2.13 (level 8); with the buff, x2.66.
- Each level **hits harder**: damage goes evenly from the base **3 at level 1 to 12 at level 10**
  (3, 4, 5 ... 12), knockback up to 2.5x and the burn chance up to 100%.
- **Enhanced**: 9 seconds and one level higher.

The log says `Trailblazer: using Searing Rush's fire` the first time it lights the trail,
`Trailblazer: lit for 6 s` when cast (and, once, the level table: `level 1 at x1.00 base run
speed, one level every +14.4%, level 10 at x2.30`), `Trailblazer: run speed X of base Y (xF; ...)`
the first time it reads your speed, `Trailblazer: leaving a trail` on the first patch, and
`Trailblazer: speed xS -> level N of 10: D damage a hit, knockback K, burn B%, size Z` every time
the level changes (the damage includes your own damage bonuses). If a hit can't be made it says
`Trailblazer: couldn't make a fire burst at level N: ...`.

## Settings

`BepInEx\config\mdbailey94.wol.trailblazer.cfg`:

| Section | Setting | Default | What it does |
| --- | --- | --- | --- |
| Trail | `Duration` / `EnhancedDuration` | 6 / 9 | Seconds your feet stay ablaze |
| Trail | `AuraSize` | 2 | Size of the ring of fire round the wizard |
| Trail | `AuraInterval` | 0.15 | Seconds between the ring's hits |
| Trail | `PatchSpacing` | 0.6 | Distance moved between patches of the trail |
| Trail | `TrailLinger` | 1.5 | Seconds each patch keeps burning |
| Trail | `TrailHitSize` | 1.6 | Size of each patch's hit |
| Trail | `TrailHitInterval` | 0.3 | Seconds between each patch's hits |
| Trail | `FlameAmount` | 2 | How many flames flicker on each patch |
| Trail | `SearingRushFire` | true | Burn with Searing Rush's fire columns (off: plain flames) |
| Trail | `FlameSize` | 1 | Size of all the flames (half size at level 1, full at level 2, +10% a level) |
| Trail | `PeakSpeedPercent` | 230 | Speed, as % of base run speed, at which the fire peaks (level 10) |
| Trail | `PeakDamageMultiplier` | 4 | Damage at level 10 as a multiple of `Damage` |
| Balance | `Damage` | 3 | Damage per hit at level 1 |
| Balance | `Knockback` | 8 | Knockback at level 1 |
| Balance | `BurnChance` | 0.5 | Burn chance at level 1 (0 to 1) |
| Balance | `Cooldown` | 12 | Cooldown in seconds |

**Trail** settings take effect straight away; **Balance** settings after restarting the game.

0.7.0 removed `SpeedStep` and `SprintBonus` (the levels are set by `PeakSpeedPercent` now); if
they're still in your .cfg they're ignored. Older `Damage`, `Knockback` and `BurnChance` values
carry over unchanged, but now mean the level-1 numbers under the new scaling.

### Changes in 0.7.0

- **Fixed: no damage at high speed.** The game only keeps five stat levels per arcana and cut the
  ten-level stat arrays down to five, so from level 6 up every hit silently had no attack. Hits are
  now made at stat level 1 and scaled by the speed level in the mod, so every level hits.
- Sprinting was misread as +100% speed (the game's run speed-up *time*, 2 s, was taken for the
  bonus); it's +40%. The levels are now by speed alone, from base run speed up to
  `PeakSpeedPercent`.
- Damage per level goes 3 to 12 (was 3 to 6); peak needs a stacked speed build.
- The "three hits a second" limit now actually applies between the ring and the trail (it was set
  on the wrong immunity field).

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLTrailblazer.zip`
from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it
into your Wizard of Legend folder. It shows up in the arcana shop (or straight away with Unlock Mod
Content on).
