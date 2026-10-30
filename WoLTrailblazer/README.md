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

## 0.10.4: the comet no longer refills its own gauge (asked for by Matt)

- The signature's hits (the ignition, the trail, the patches' eruptions and the final blast,
  including patches still burning for a few seconds after) are marked as a signature's, as the
  game marks its own, so they no longer refill the signature gauge. Up to 0.10.3 its many hits
  filled it again almost at once. Its look and damage are unchanged.

## Signature: the comet (0.10.0, asked for by Matt)

Trailblazer has a **signature version** now (the spellbook's Signature page; unlocked along with
the arcana, `[General] SignatureUnlocksWithArcana`):

1. **Comet**: for **5 s** (`[Signature] Duration`) the room darkens to half its light and the
   camera pulls back 15%. Your fire burns **three levels hotter** (the enhanced level and two
   more), its trail patches hit **twice as wide**, and its flames and fire columns are half again
   as big. The ring round you is 1.5x as wide.
2. **Eruption**: when it burns out, every patch of the trail still burning **erupts in a pillar
   of fire**, oldest first, a beat apart. Each pillar hits for twice a level-10 hit (24 at the
   default balance; `EruptionMultiplier`).
3. **Blast**: a quarter second later a blast goes off round you (radius 3) for four times a
   level-10 hit (48; `FinalMultiplier`), with a 5-frame hit-stop, a flash, a heavy shake and a
   moment of near-frozen time. Then the camera and light ease back.

The camera and light always go back (`Trailblazer: signature restored (...)`).

**0.10.1 (Matt)**: the detonations start earlier. Each trail patch **erupts in its pillar 0.5 s
after it's dropped**, while the comet is still burning, so the trail goes up in a rolling wave
behind you. When it burns out, any patch not yet erupted goes too, then the blast. And you run
**+40% faster for the whole signature** (the comet, the eruption and the blast), which pushes the
fire's speed level up as well (a sprint with the boost is level 6 before the comet's three
extra levels, so the comet is at level 9 or 10).

**0.10.3**: the `speed xN -> level N` log line is written only when the fire reaches a new
highest level in a cast; it was written on every level change, about 1.4 lines a second while
running and three quarters of a long session's log.

**0.10.2 (Matt)**: walked back slightly: each patch erupts **0.8 s** after it's dropped (was
0.5), and the boost is **+25%** (was +40%); a boosted sprint is level 5 before the comet's three
extra levels, so the comet burns at level 8 or 9.

## Look

Since **0.8.0** more fire is layered on top of the game's flames and Searing Rush's columns (which
look as before). Since **0.8.1** it's in the game's own fire style (flat cream, yellow, amber and
orange sampled from the game's fire sheets, ragged licking edges, no outlines) and uses the game's
own sprites and effects where they fit:

- **The cast**: a ring of flame bursts out over the floor from the stamp with the game's flame
  circle burst and embers flying, and the game's own scorch mark is left under it.
- **While ablaze**: flame tongues lick up from your feet: two at a walk, up to five and bigger as
  the speed level climbs, leaning back as you run. Embers flick off behind you, more the faster you
  go. The ring of fire's hit circle is invisible unless `[Look] RingOfFire` is on, which draws it on
  the floor at its size.
- **The trail**: each patch burns with the game's own flames (its meteor's flame, from the
  MeteorStrike sheet, taller at higher levels) and leaves the game's burn mark with a glow in it
  that cools from cream to orange and fades. When a patch goes out its flames burst into sparks
  as the game's do.
- **Burning out**: a column of fire flares up round the wizard and breaks into flecks, with a burst
  of embers, the game's flame burst, smoke and burn mark.

The drawn parts are made in code at the game's pixel size and everything is pooled (capped, the
oldest reused; put away when the fire's done or the level changes). Where the game's flames or burn
marks aren't loaded, drawn stand-ins in the same style are used. `Drawn` in `[Look]` turns it all
off (from the next cast). Purely visual: the hits, sizes and timings are as before.

They log one line each the first time they play, for example `Trailblazer: visual 'body flames'
ready (54 sprites, ppu 16)` (also `ignition burst`, `ring of fire`, `embers`, `char marks`,
`trail flames`, `dying flames` and `flare-up`), and once which sorting layers they draw on:
`Trailblazer: drawn effects sort on 'Shadow' (#n) (floor marks) and 'TargetLineGround' (#n) ...`. If
one can't be made it says `Trailblazer: visual '...' skipped: ...` once and the fire goes on
without it. Which of the game's sprites are used is logged once:
`Trailblazer: visual 'trail flames' uses the game's sprites MeteorStrike flame tiles` (or `...: the
game's ... isn't loaded here, drawing it instead`) and `Trailblazer: char marks use the game's burn
marks (...) with a drawn glow`.

## Log

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
| Trail | `AuraSize` | 2 | Size of the (invisible) ring of fire round the wizard that hurts enemies |
| Look | `RingOfFire` | false | Draw the ring of fire on the floor |
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
| Look | `Drawn` | true | The drawn fire (flames on the wizard, embers, ring, trail flames and char marks, cast burst, flare-up) |
| Balance | `Damage` | 3 | Damage per hit at level 1 |
| Balance | `Knockback` | 8 | Knockback at level 1 |
| Balance | `BurnChance` | 0.5 | Burn chance at level 1 (0 to 1) |
| Balance | `Cooldown` | 12 | Cooldown in seconds |

**Trail** and **Look** settings take effect straight away (`Drawn` from the next cast); **Balance**
settings after restarting the game.

0.7.0 removed `SpeedStep` and `SprintBonus` (the levels are set by `PeakSpeedPercent` now); if
they're still in your .cfg they're ignored. Older `Damage`, `Knockback` and `BurnChance` values
carry over unchanged, but now mean the level-1 numbers under the new scaling.

### Changes in 0.9.3

- Lighter again (task sheet #26): each of the trail's hits is a new copy of the game's flame
  burst, and a full trail asked for over a hundred a second, nearly all on empty floor. A patch
  now makes its hit only when an enemy (or a boss) is within reach of it, so enemies are hit as
  before; the trail no longer burns crates and pots it is merely lying next to (the ring round
  the wizard still does). In PvP every patch hits as before.
- The trail's borrowed flame frames are kept instead of looked up on every patch, and when the
  game's flame sheet isn't loaded (it comes with Meteor), all lookups share one search of the
  loaded textures every 20 s instead of one search each.

### Changes in 0.9.2

- Faster trail (task sheet #26, frame-rate drops): each patch used to copy Searing Rush's fire
  column afresh and strip it down (several searches of its parts and a component removal each),
  15 to 20 times a second at a sprint. The stripped columns are now kept and lit again for later
  patches, so the trail looks the same for a fraction of the work.

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


## 0.10.5

Signature damage retains its signature flag through bursts and delayed hits, so it cannot generate signature charge. Standard/enhanced casts still charge normally. Trailblazer patches retain the originating cast flag across recasts and long linger settings.
