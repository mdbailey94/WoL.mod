# Giant Weapons

The giant weapon set's basics built by WoL Modder (Matt, 2026-11-03). WoL Player's Giant Air Spear
and Buckler are their own mods (WoLGiantSpear, WoLBuckler); Flaming Whip is WoLFlamingWhip.

## Giant Ice Sword (#83, Water basic, tier 2; reworked in 0.4.0, #93)

- A **slow 3-hit combo** (the wizard's swings play at 65% pace). The drawn blade is the game's own
  ice boss greatsword, turned by the mod so it sweeps exactly where the hit box is.
- **Hit 1:** a **45°** swing **clockwise**, **2.2 u** long, **12** damage, **20%** chill.
- **Hit 2:** the same **45°** swing back **counterclockwise**.
- **Hit 3:** always one **360° spin**, clockwise from the aim over **0.5 s**. The hit follows the
  blade: the circle is cut into 4 slices and each enemy is hit once, as the blade passes it.
  - **Tapped:** **2.5 u** radius, **18** damage, **30%** freeze.
  - **Held** as hit 3 starts (the wizard glows ice blue; at least **0.4 s**, full at **1 s**): wider
    with the charge, **22** damage (**28** and **3 u** at full charge), **40%** freeze (**50%** full).
- **Enhanced:** the spin is **30%** wider and leaves a **frost ring** for **1.5 s** that slows enemies.

## Giant Lightning Flail (#84, Lightning basic, tier 2; 0.5.0, #92)

- A **slow 3-hit combo** (**63%** pace since 0.5.0, 10% slower than before).
- **Hits 1 and 2:** the **spiked ball itself** goes once round the wizard at **2 u** over
  **0.45 s** (hit 1 counterclockwise, hit 2 clockwise, starting at the aim), on its lightning
  chain, with a faint motion-blur ring behind it. The hit follows the ball (4 slices, each enemy
  once): **10** damage, **25%** shock.
- **Hit 3:** a **0.4 s** wind-up, then a **slam** **2.5 u** ahead in a **1.5 u** radius: **20**
  damage, always shocks, knockback, camera shake and a thunder thud.
- **Enhanced:** the slam arcs lightning to up to **3** enemies within **4 u**, **6** damage each.

Config keys renamed in 0.4.0/0.5.0 so the new numbers reach existing installs: `[Ice Sword]`
SwingAngle, SpinSlices, SpinTurnTime, SwingTurnTime, SwordAngleOffset (turns the drawn blade if it
doesn't line up with the aim); `[Balance]` SwordTapSpinDamage, SwordChargedSpinDamage,
SwordFullSpin360Damage; `[Flail]` SwingPace, CircleTime, CircleSlices, BlurAlpha.

Neither can have a signature: the game's spellbook puts every basic in the basic slot before it
looks for a signature. The hits are the game's own ice and lightning bursts with each arcana's
stats, so gear, crits, statuses and talents apply. The Ice Sword shows the game's own ice boss greatsword; the Flail is WoL Art's (Finished Art/Giant weapons, 2026-11-03).

## Settings

`BepInEx\config\mdbailey94.wol.giantweapons.cfg`: `[Ice Sword]`, `[Flail]` (next attack) and
`[Balance]` (after a restart); `[General]` turns each one's shop offer off.

**0.3.0** (#84): WoL Art's flail (Matt: a black ball with white spikes on an electric chain): the circle swings play its ring round the wizard with the lightning chain out to the ball; the slam reaches the ball and chain out to the spot over the wind-up, then its impact plays there. Art's icons for both arcana.

**0.2.0** (Matt: 'reusing the ice boss's sword would be good'): the Ice Sword's swings, chop and spin show the game's own ice boss greatsword (its IceSword prefab, as the game lends it to a player arcana), appearing, swinging and shattering into ice; [Ice Sword] SwordScale and SwordSwingSpeed size and pace it. The hits are unchanged.

**0.1.0** (#83, #84): first version.
