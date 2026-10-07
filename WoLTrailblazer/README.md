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
  fire** round them hits any enemy touching it: about **3 damage**, a **small knockback** and a
  **chance to burn** (50%), moving or not.
- As they move (running, dashing, or carried by any movement arcana) they leave **patches of
  fire** behind them every short distance. Each burns on the ground for about 1.5 seconds and
  hurts enemies who walk into it. It burns with **Searing Rush's fire** (the game's own fire
  columns), small at normal speed and growing to full size as the hits get stronger. The same enemy is hit at most about three times a second, ring
  and trail together.
- **It grows with your speed** (relics and sprinting count) over **ten levels**: running at base
  speed is level 1, the fire at **half size**; sprinting at base speed is level 2, **full size**;
  then **every 5% faster** is one more level and **+10% size**, up to level 10 (1.8x). Each level
  hits harder too: +12% damage, +15% knockback and +6% burn chance per level.
- **Enhanced**: 9 seconds and one level higher.

The log says `Trailblazer: using Searing Rush's fire` the first time it lights the trail,
`Trailblazer: lit for 6 s` when cast, `Trailblazer: fire bursts working` on the first
hit, `Trailblazer: leaving a trail` on the first patch, `Trailblazer: a sprint is +X% speed`, and
`Trailblazer: speed xA (sprint xB) -> level N` as the level changes.

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
| Trail | `FlameSize` | 1 | Size of all the flames (small at normal speed, growing with damage) |
| Trail | `SpeedStep` | 0.05 | Above a base-speed sprint, how much faster for each further level |
| Trail | `SprintBonus` | 0 | How much faster a sprint is than a run (0 = read it from the game) |
| Balance | `Damage` | 3 | Damage per hit at normal speed |
| Balance | `Knockback` | 8 | Knockback at normal speed |
| Balance | `BurnChance` | 0.5 | Burn chance at normal speed (0 to 1) |
| Balance | `Cooldown` | 12 | Cooldown in seconds |

**Trail** settings take effect straight away; **Balance** settings after restarting the game.

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLTrailblazer.zip`
from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it
into your Wizard of Legend folder. It shows up in the arcana shop (or straight away with Unlock Mod
Content on).
