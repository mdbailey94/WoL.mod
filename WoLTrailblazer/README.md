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
  hurts enemies who walk into it. The flames are small at normal speed and grow, with more of
  them, as the hits get stronger. The same enemy is hit at most about three times a second, ring
  and trail together.
- **It grows with your run speed** after relics and other bonuses: every +15% run speed is one
  level stronger, up to five levels. Each level means a bigger ring and trail, +25% damage, +30%
  knockback and +12.5% burn chance (sure burns from level 5).
- **Enhanced**: 9 seconds, one level stronger and bigger fire.

The log says `Trailblazer: lit for 6 s` when cast, `Trailblazer: fire bursts working` on the first
hit, `Trailblazer: leaving a trail` on the first patch, and `Trailblazer: run speed X of base Y`.

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
| Trail | `FlameSize` | 1 | Size of all the flames (small at normal speed, growing with damage) |
| Trail | `SpeedPerLevel` | 0.15 | Extra run speed per stronger level |
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
