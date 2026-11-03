# Flaming Whip

> **Flaming Whip** (Fire basic arcana, tier 2, giant weapon set): heave a giant blazing whip
> overhead and crack it down on the spot three steps ahead: too close or too far and it misses!

- **One big crack per press** (no combo).
- **Cast time:** the wizard winds the whip up overhead (the game's arms-up pose, played 30%
  slower for a heavy feel), and the crack lands **0.3 s** after the press.
- **Where it hits:** a **1-tile** spot exactly **3 tiles** out along your aim, measured from the
  hand. Nothing between you and the spot is hit, so you can be too close or too far.
- **The crack:** **18** damage, **50%** burn, knockback; a camera shake, a soft crack and flame,
  the lash drawn out to the spot through bursts of fire, a fire ring and a scorch mark on it.
- **Enhanced:** the spot is **1.5 tiles** wide and leaves a line of fire across it for **1.5 s**
  (**2** damage a tick).
- **No signature:** the game's spellbook puts every basic in the basic slot before it looks for a
  signature, so a basic can't be one.

The hits are the game's own fire bursts with this arcana's stats, so burn, gear, crits and
talents apply. Art by WoL Art (Finished Art/Prickly Ballet + Flaming Whip, 2026-11-02); a bigger
overhead-arc crack is being drawn for #81/#86.

## Settings

`BepInEx\config\mdbailey94.wol.flamingwhip.cfg`:
- `[Crack]` (next press): `Distance`, `SpotSize`, `WindUp`, `WindUpPace`, `Shake`.
- `[Enhanced]` (next press): `SpotSize`, `FireLineTime`, `FireLineTick`.
- `[Balance]` (after a restart): `BigCrackDamage`, `FireLineDamage`, `BigCrackBurnChance`,
  `BigCrackKnockback`.

**0.4.0** (Matt's play-test): reach 2 to 3 tiles (an older config is moved over once), quieter (no low thud; the crack and flame at about half volume), and more like a spell: fire gathers at the hand during the wind-up, flames burst along the lash and round the spot, and it leaves a scorch mark.

**0.3.0** (#81/#86): redesigned as one big, heavy crack on a spot 2 tiles out, with a cast time.
The old [Whip] and lash/crack keys in an existing config are left unused.

**0.2.0** (#78): WoL Art's redrawn lash and crack burst; smaller crack ring, wind-up pose, crack sound.

**0.1.0** (#69): first version, a 3-hit combo.
