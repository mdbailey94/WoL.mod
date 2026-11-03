# Cactus Waltz

> **Cactus Waltz** (Earth dash arcana, tier 2): dash away and leave a cactus behind you,
> needling everyone close!

- The **dash** is the game's normal dash: same distance and speed, evade, and the dash relics count.
- Where the dash started, the **base game's own cactus** (the one from its Earth arcana Prickly
  Ballet) pops out of the ground and stays put for **3 s**, needling every enemy in its reach every
  **0.4 s** for **4** damage with a small knockback, then plays its own death animation.
- At most one cactus every **6 s**; dashes in between are plain dashes.
- **Enhanced:** the cactus stays **4 s** and comes back every **4.5 s**.
- **No signature:** the game's spellbook puts every dash in the dash slot before it looks for a
  signature, so a dash can't be one.

The hits use this arcana's stats through the game's own attack, so gear, crits and talents apply.
The mod's folder (WoLPricklyBallet), GUID and skill ID (PricklyBallet) keep their 0.1.0 names, so
installs, configs and saves carry over: a save holding an arcana ID the game no longer knows has
its whole loadout reset.

## Settings

`BepInEx\config\mdbailey94.wol.pricklyballet.cfg`:
- `[Cactus]` (next dash): `Lifetime`, `Every`, `HitEvery`.
- `[Cactus Enhanced]` (next dash): `Lifetime`, `Every`.
- `[Balance]` (after a restart): `PrickDamage`, `PrickKnockback`, `Cooldown`.

**0.2.0** (#77): renamed Cactus Waltz (the base game already has a Prickly Ballet); the cactus is
now the game's own instead of WoL Art's dancer, a cactus every 6 s (enhanced 4.5 s), and the
8-needle burst is gone (the game's cactus has its own needles and death).

**0.1.0** (#68): first version, as Prickly Ballet.
