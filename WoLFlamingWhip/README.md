# Flaming Whip

> **Flaming Whip** (Fire basic arcana, tier 2): lash out with a blazing whip that reaches far,
> then crack it overhead to scorch everything around you!

- A **3-hit combo** like the game's basics.
- **Hits 1 and 2:** a lash **3.5 u** long and **0.6 u** wide along your aim, **7** damage to every
  enemy on it, **25%** chance to burn, small knockback.
- **Hit 3:** a clear wind-up, then the whip cracks overhead into a **1.8 u** ring round you, with
  a bright crack burst and a sharp crack sound: **12** damage, **50%** burn, with knockback.
- Reach is its strength; up close it's weaker than the sword basics.
- **Enhanced:** every lash leaves a line of fire on the floor for **1.5 s** (**2** damage a tick).
- **No signature:** the game's spellbook puts every basic in the basic slot before it looks for a
  signature, so a basic can't be one.

The hits are the game's own fire bursts with this arcana's stats, so burn, gear, crits and
talents apply. Art by WoL Art (Finished Art/Prickly Ballet + Flaming Whip, 2026-11-02).

## Settings

`BepInEx\config\mdbailey94.wol.flamingwhip.cfg`:
- `[Whip]` (next attack): `LashLength`, `LashWidth`, `CrackRadius`.
- `[Enhanced]` (next attack): `FireLineTime`, `FireLineTick`.
- `[Balance]` (after a restart): `LashDamage`, `CrackDamage`, `FireLineDamage`, `LashBurnChance`,
  `CrackBurnChance`, `LashKnockback`, `CrackKnockback`.

**0.2.0** (#78): WoL Art's redrawn lash (an S-wave runs down it, then it snaps straight with a
spark at the tip) and crack burst; the crack's ring is smaller (2.5 u to 1.8 u; a 0.1.0 config
is moved over on first launch), with a wind-up pose and a crack sound.

**0.1.0** (#69): first version.
