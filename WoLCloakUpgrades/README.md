# Cloak Upgrades (Wizard of Legend)

Spend chaos gems on **permanent** upgrades for your cloaks, at the wardrobe in your house.

## How it works

Everything happens in the wardrobe's own screen. Highlight a cloak and its info box shows its tier
and the next upgrade, e.g. `Tier 1/4: bonuses +25%` / `Press Y to upgrade: 200 gems`. The box's
text shrinks to fit (down to a third of its size), and for cloaks with a long list of stats the
upgrade takes a single line (`Tier 1/4: bonuses +25% - Y: 200 gems`). Press **Y** on a controller (Triangle on PlayStation pads) or **U** on the
keyboard and the game's own yes/no box asks to confirm; a purchase is announced by the game's notice
banner. If the yes/no box can't be used, press the button a second time instead. You can only
upgrade cloaks you've unlocked.

| Tier | Cost | The cloak's bonuses |
|------|------|---------------------|
| 1 | 100 gems | +25% |
| 2 | 200 gems | +50% |
| 3 | 400 gems | +75% |
| 4 | 800 gems | **doubled** (+100%) |

That's 1500 gems to max out one cloak. Each cloak is upgraded separately and keeps its tier for
good: across runs, and after quitting the game.

**Tailored stats.** Some bonuses would be too strong doubled, so they only go up to **half again**
(+12.5% a tier, +50% at tier 4): movement and running speed, evasion, armour, cooldown reduction,
and extra gold or gems. A cloak's **drawbacks are never made worse**, and effects that aren't a
plain number (on/off effects, Pride, fall protection) stay as they are.

The upgraded numbers show in the wardrobe's own description of the cloak, and the log lists each
change the first time it applies (`... tier 2: Damage bonus x1.5 (...)`).

## Settings

`BepInEx/config/mdbailey94.wol.cloakupgrades.cfg`:

- `[General] Enabled`: turn the upgrades on or off. Off, cloaks have their normal stats; your tiers
  are kept for when you turn it back on.
- `[Tiers]`: each cloak's tier, by the game's cloak ID (0-4). You can edit these by hand.

## Install

Download `WoLCloakUpgrades.zip` from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest)
and unzip it into your Wizard of Legend folder. Only BepInEx is needed.
