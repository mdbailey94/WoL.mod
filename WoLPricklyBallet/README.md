# Prickly Ballet

> **Prickly Ballet** (Earth dash arcana, tier 2): dash away and leave a cactus dancer pirouetting
> behind you, pricking everyone close, then bursting into needles!

- The **dash** is the game's normal dash: same distance and speed, evade, and the dash relics count.
- Where the dash started, a **cactus dancer** rises out of the ground and pirouettes for **3 s**
  (at most one every **4 s**; dashes in between are plain dashes).
- While it spins it **pricks** every enemy within **1.2 u** every **0.4 s** for **4** damage,
  with a small knockback.
- When its time is up it **bursts** into **8 needles** flying out **4 u**, **6** damage each
  (one hit per needle; walls stop them).
- **Enhanced:** the dancer spins **4 s** and comes back every **3 s**.
- **No signature:** the game's spellbook puts every dash in the dash slot before it looks for a
  signature, so a dash can't be one.
- **Not yet:** the dancer doesn't draw enemies' attention yet (Matt's spec: enemies within 4 u
  attack it for its first 1.5 s).

The hits are the game's own bursts with this arcana's stats, so gear, crits and talents apply.
Art by WoL Art (Finished Art/Prickly Ballet + Flaming Whip, 2026-11-02).

## Settings

`BepInEx\config\mdbailey94.wol.pricklyballet.cfg`:
- `[Dancer]` (next dash): `Lifetime`, `Every`, `Reach`, `PrickEvery`, `Needles`, `NeedleRange`, `NeedleSpeed`.
- `[Enhanced]` (next dash): `Lifetime`, `Every`.
- `[Balance]` (after a restart): `PrickDamage`, `NeedleDamage`, `PrickKnockback`, `NeedleKnockback`, `Cooldown`.

**0.1.0** (#68): first version.
