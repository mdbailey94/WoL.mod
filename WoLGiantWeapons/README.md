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

- A **slow 3-hit combo** (**60%** pace since 0.5.0: each swing about **17%** longer than before).
- **Hits 1 and 2:** the **spiked ball itself** goes once round the wizard at **2 u** over
  **0.45 s** (hit 1 counterclockwise, hit 2 clockwise, starting at the aim), on its lightning
  chain, with a faint motion-blur ring behind it. The hit follows the ball (4 slices, each enemy
  once): **10** damage, **25%** shock.
- **Hit 3:** a **0.4 s** wind-up, then a **slam** **2.5 u** ahead in a **1.5 u** radius: **20**
  damage, always shocks, knockback, camera shake and a thunder thud.
- **Enhanced:** the slam arcs lightning to up to **3** enemies within **4 u**, **6** damage each.

0.11.0 (#106, Matt's flail rework):
- **Bigger ball:** drawn 1.5x until WoL Art's 24 px `flail_head_big` lands (then 1:1).
- **Hits 1-2: swooping 360° swings:** the ball dips low and wide in front and rises behind, reach
  **2.8 u** (was 2.6; old configs on 2.6 move over once); hits still cover the whole circle.
- **Hit 3: charge, then slam in front.** Let go after hit 2, then HOLD: the ball whirls above the head,
  faster and brighter as it charges (min **0.3 s**, full **1.2 s**); let go to drop it on the spot
  2.5 u ahead in **0.25 s**. Charged: radius 2.0 to **2.6 u** with the charge, **34** damage and an
  always-shock at full (level 4); otherwise the tap slam: the over-the-top swing, **20** damage,
  2.0 u, now **50%** shock (was 100%). Bigger shake with the charge.

0.10.0 (#105, Matt: "brace = charge, swing FAST"):
- **Ice Sword brace combo:** each press is a **brace** then a **snap**. Hit 1: the wizard steps
  **0.6 u** forward over a **0.35 s** brace with the blade drawn back on one side, then it snaps 45°
  clockwise in **0.12 s** (fast out of the brace, easing at the end; the hit lands as the blade crosses
  the aim). Hit 2: the same from the opposite side, counterclockwise. Hit 3: a **0.5 s** brace
  (0.4 u step), then the 360° spin in **0.45 s**; holding the button through it charges the spin
  (22, full 28) as before. Damage, reach and the 75° hit arc unchanged. A dash cancels a brace.

0.9.0 (Matt's play-test: "the flail was not hitting enemies", "hit boxes are too thin"):
- **Hits land on the body:** every hit used to be a small burst at the enemy's FEET (its pivot),
  while the hurt box sits up on the body, so many flail and sword hits missed. Enemies are now found
  and hit at the centre of their hurt box, with a burst wide enough to touch it (0.35 u).
- **Wider Ice Sword swings:** reach **2.6 u** (was 2.2) and a **75°** hit arc around the drawn 45°
  sweep. Config files still on 2.2 move over once.

0.8.0 (#101: crates and pots, cleanup):
- **Crates, pots, plants and other breakables** in the swing arc, the spin's slices and the flail's
  circle are hit directly (they used to break only when a burst on an enemy happened to reach them).
  The slam already broke them.
- Changing `Pace` / `SwingPace` now takes effect on the next attack (it needed a restart).
- One timer object per scene holds the delayed hits (was a new object per hit); the sword's blade
  is looked up 10 times a second at most; unused `ChopReach` and `SwordSwingSpeed` settings removed.

0.7.0 (#101 audit fixes):
- Ice Sword: the spinning blade stays at your hand as you move, so its hits (centred on you) never
  land where no blade is drawn. The spin charges only on a fresh hold (let go after hit 2, then
  hold again); holding attack through the combo gives a tapped spin. The charge glow no longer
  wipes other colours on the wizard.
- Hit areas count the enemy's body (0.3 u) on the reach and the arc edges, so big enemies the
  blade or ball visibly crosses are hit.
- Delayed hits (swings, spin slices, circle slices, slam) don't land after the wizard dies.
- Enhanced Flail arcs go to the nearest enemies.

0.6.0 (#95, #97, Matt's play-test):
- **Ice Sword:** the blade is now steered by where it actually points, so hit 1 really sweeps
  **clockwise** and hit 2 **counterclockwise** for every aim. **Heavy:** each swing takes **0.32 s**
  and the spin **0.7 s**, on a slow-start curve that speeds up (`Momentum` 2); a swing's hit lands as
  the blade comes through the middle, and the spin's slices as the blade passes them.
- **Flail:** bigger hit areas: the circle swings reach **2.6 u** (was 2) and the slam is **2 u**
  across its radius (was 1.5). The third hit is an **over-the-top** swing: over a **0.6 s** wind-up
  the ball rises slowly to **1.8 u** above the head, then drops fast in an arc onto the spot.
  No new art: Art's ball and chain draw the path.

0.5.1: each slice's hit is a tighter burst on the enemy, so a neighbour in the next slice isn't hit twice.

Timings now: Ice swings 0.32 s, spin 0.7 s (heavy curve); Flail circle 0.45 s, slam wind-up 0.6 s.
Keys left unused in older config files (safe to delete): `[Ice Sword]` SwingArc, SpinTurnTime,
SwingTurnTime, SpinTime, SpinHits, ChopReach, SwordSwingSpeed; `[Flail]` Pace, CircleRadius, SlamRadius,
SlamWindUp; `[Balance]` SwordChopDamage, SwordSpinDamage, SwordFullSpinDamage.

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
