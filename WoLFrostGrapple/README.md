# Frost Grapple (Wizard of Legend)

A **Water standard arcana** (tier 2) built on Matt's request (2026-11-04): throw an ice pickaxe,
then pull yourself to whatever it bites. Needs BepInEx and LegendAPI.

## How it plays (0.1.0)

- **Throw:** the pickaxe flies along your aim at **20 u/s** for up to **7 u**.
- **Bites an enemy:** **8** damage, **30%** chill. The ice chain snaps tight and you're **pulled** to
  the enemy at **24 u/s** (at most **0.5 s**). You can't be hit while pulled and you float over pits.
  You land **0.6 u** short of them in an ice burst: **16** damage in a **1.2 u** radius, **40%** freeze,
  knockback.
- **Hits a wall:** you're pulled to the wall (no damage): a way to move.
- **Misses:** the pick flies back and only **half** the cooldown is spent.
- **Cooldown:** 5 s, counted from the landing.
- **Enhanced** (one upgrade): the landing burst is **30% wider** (1.56 u).
- **Signature** (cinematic): the stage darkens and the camera closes in. The pick leaps from enemy to
  enemy, up to **3**, and you zip after it each time: a **12**-damage burst and a short hit-stop at each
  stop, then the **finale** at the last one: **24** damage in **1.8 u**, **60%** freeze, a 5-frame
  hit-stop, a white flash, the stage lighting up and a moment of slow-motion.

Every number is in `mdbailey94.wol.frostgrapple.cfg` (`[Throw]`, `[Pull]`, `[Enhanced]`,
`[Signature]` apply on the next cast; `[Balance]` after a restart).

## Art

WoL Art's sheets are picked up by name from `Assets/` (spec: Log - Modder 2026-11-04 11:45):
`ice_pickaxe_spin` (8 x 16x16), `ice_pickaxe_stuck` (2 x 16x16), `ice_link` (2 x 4x4),
`frost_grapple_impact` (6 x 40x40) and `_enh` (6 x 50x50), `frost_grapple_icon` (18x18). Until they
exist, a code-drawn ice diamond and chain dots stand in, and the game's own ice bursts show the hits.
