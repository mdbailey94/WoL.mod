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

## Changes in 0.6.0 (#98)

- **Bigger hit area:** the crack's spot is **1.5 tiles** across (was 1), enhanced **2 tiles** (was 1.5),
  still 3 tiles out. New keys `[Crack] HitSpot` / `[Enhanced] HitSpot` (the old SpotSize keys are unused).
- The crack art matching the spot is picked automatically, so 1.5 tiles draws Art's _enh crack 1:1;
  the 2-tile enhanced spot uses it at 1.33x until a native `flamingwhip_crack_huge` (8 x 48x48) exists.

## Changes in 0.5.0 (#91, WoL Art's v3 "spell look")

- The wind-up now draws Art's **whip_windup** (the whip coiling back over the head with fire
  gathering at the hand), then **whip_arc** sweeps it overhead; the arc's strike frame lands
  exactly when the crack fires (0.3 s after the press). Aiming left the strips are flipped so the
  coil stays above; aiming down-screen the coil is drawn behind the wizard.
- The crack uses Art's native **flamingwhip_crack_big** (1-tile spot) or **_enh** (1.5-tile spot),
  so it is drawn 1:1 instead of scaled.
- Any strip that isn't in `Assets/` yet falls back to the 0.4.0 look. Art's floor sigil
  (flamingwhip_sigil) is not hooked: waiting on Matt's OK.

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
