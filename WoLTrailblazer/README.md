# Trailblazer (Wizard of Legend)

A new **Fire dash arcana**, added with [LegendAPI](https://github.com/yekoc/LegendAPI).

> **Trailblazer**: Running, dashing and every movement arcana leave a trail of fire, scorching and
> shoving aside any enemy you run into. The faster you are, the bigger it burns!
>
> *Enhanced: Burns bigger and hotter!*

- Equip it as your dash. The dash itself is a normal dash.
- While it's equipped, whenever your wizard is moving (running, dashing, or carried by any
  movement arcana) they leave **flames on the ground behind them**, and anyone they run into is
  hit by a small fire burst: about **3 damage**, a **small knockback** and a **chance to burn**
  (50%). The same enemy is hit at most about three times a second.
- **It grows with your run speed** after relics and other bonuses: every +15% run speed is one
  level stronger, up to five levels. Each level means more flames, a wider hit, +25% damage, +30%
  knockback and +12.5% burn chance (sure burns from level 5).
- **Enhanced**: one level stronger and a bigger trail.

The log says `Trailblazer: the trail is on` when it starts following your wizard, and
`Trailblazer: run speed X of base Y` the first time it reads your speed.

## Settings

`BepInEx\config\mdbailey94.wol.trailblazer.cfg`:

| Section | Setting | Default | What it does |
| --- | --- | --- | --- |
| Trail | `MinSpeed` | 1.5 | How fast you must be moving (units a second) to leave fire |
| Trail | `FlameInterval` | 0.04 | Seconds between puffs of flame |
| Trail | `FlameAmount` | 2 | Flames per puff at normal speed |
| Trail | `TrailLinger` | 0.5 | Roughly how long the flames last |
| Trail | `HitInterval` | 0.1 | Seconds between scorching hits while moving |
| Trail | `HitSize` | 0.8 | Size of the hit at normal speed |
| Trail | `SpeedPerLevel` | 0.15 | Extra run speed per stronger level |
| Balance | `Damage` | 3 | Damage per hit at normal speed |
| Balance | `Knockback` | 8 | Knockback at normal speed |
| Balance | `BurnChance` | 0.5 | Burn chance at normal speed (0 to 1) |
| Balance | `DashCooldown` | 0.6 | The dash's cooldown |

**Trail** settings take effect straight away; **Balance** settings after restarting the game.

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLTrailblazer.zip`
from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it
into your Wizard of Legend folder. It shows up in the arcana shop (or straight away with Unlock Mod
Content on).
