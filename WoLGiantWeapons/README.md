# Giant Weapons

The giant weapon set's basics built by WoL Modder (Matt, 2026-11-03). WoL Player's Giant Air Spear
and Buckler are their own mods (WoLGiantSpear, WoLBuckler); Flaming Whip is WoLFlamingWhip.

## Giant Ice Sword (#83, Water basic, tier 2)

- A **slow 3-hit combo** (the wizard's swings play at 65% pace).
- **Hits 1 and 2:** a wide **120°** arc **2.2 u** long in front, **12** damage each, **20%** chill.
- **Hit 3, tapped:** an overhead chop **2.5 u** long, **18** damage, **40%** freeze, knockback.
- **Hit 3, held:** keep the button held as the third swing starts to **charge** (the wizard glows
  ice blue; at least **0.4 s**, full at **1 s**), then let go to **spin**: 3 hits round you over
  0.6 s in a **2.5 u** radius (**3 u** at full charge), **10** each (**14** at full charge), up
  to **50%** freeze.
- **Enhanced:** the spin is **30%** wider and leaves a **frost ring** for **1.5 s** that slows enemies.

## Giant Lightning Flail (#84, Lightning basic, tier 2)

- A **slow 3-hit combo** (70% pace).
- **Hits 1 and 2:** the flail swings round in a full **circle**, hitting everything within **2 u**,
  **10** damage each, **25%** shock.
- **Hit 3:** a **0.4 s** wind-up, then a **slam** **2.5 u** ahead in a **1.5 u** radius: **20**
  damage, always shocks, knockback, camera shake and a thunder thud.
- **Enhanced:** the slam arcs lightning to up to **3** enemies within **4 u**, **6** damage each.

Neither can have a signature: the game's spellbook puts every basic in the basic slot before it
looks for a signature. The hits are the game's own ice and lightning bursts with each arcana's
stats, so gear, crits, statuses and talents apply. WoL Art's sprites are hooked when drawn.

## Settings

`BepInEx\config\mdbailey94.wol.giantweapons.cfg`: `[Ice Sword]`, `[Flail]` (next attack) and
`[Balance]` (after a restart); `[General]` turns each one's shop offer off.

**0.1.0** (#83, #84): first version.
