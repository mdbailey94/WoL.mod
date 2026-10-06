# Cloak Upgrades (Wizard of Legend)

Spend chaos gems on **permanent** upgrades for your cloaks, at the wardrobe in your house.

## How it works

Open the wardrobe and a **CLOAK UPGRADE** panel appears along the bottom of the screen for the
cloak you have highlighted: four pips for its tier, what it does now and at the next tier, the
price and your gems. Press **U** (keyboard) or **Y / Triangle** (controller) to buy the next tier.
You can only upgrade cloaks you've unlocked.

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
