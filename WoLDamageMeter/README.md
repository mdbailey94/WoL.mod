# Damage Meter (Wizard of Legend)

A damage-per-second readout for the training dummies in your house, for testing builds and
balancing arcana. Hit a dummy and a small panel appears in the top-right corner with:

| Stat | |
|------|-|
| DPS | Damage divided by the session's length |
| Damage | Total damage dealt, after crits, relics and other modifiers (the numbers the game shows) |
| Hits | Hits landed, with how many were critical |
| Biggest hit | With "crit" if it was one, and the arcana that did it |
| Time | First hit to last hit |
| By arcana | Your four most damaging arcana: damage, % of the total, hits |

A **session** starts on your first hit and ends after 3 seconds without one. The panel shows "(live)"
while you're hitting and "(done)" once the session is over; the final numbers stay up for 5 seconds,
then fade. Your next hit starts a fresh session.

The dummies reset themselves after a few seconds (the big number they show when they teleport back is
the damage they took since their last reset). That doesn't affect the meter, which counts each hit as
it lands. Hitting several dummies at once adds them all up; the panel then also shows how many you hit.
In co-op, both players' damage goes into the same session.

## Hotkey

Press **F3** to hide or show the panel. Sessions are still recorded in the log while it's hidden.

## Log

Each session writes a summary to `BepInEx\LogOutput.log`, plus one line per arcana (all of them,
not just the top four):

```
[Info   :Damage Meter] Session: 12.3 s, 4567 dmg, 371 DPS, 58 hits, 9 crits, biggest 210 (crit, Wind Slash), 1 target
[Info   :Damage Meter]   Wind Slash [WindSlash]: 2100 dmg (46%), 171 DPS, 30 hits, 5 crits
```

The name in brackets is the game's skill ID. Burn, poison and other damage over time show up under
their own IDs, and "Other" is damage the game didn't tag with a skill.

## Install

Download `WoLDamageMeter.zip` from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest)
and unzip it into your Wizard of Legend folder. Only BepInEx is needed, not LegendAPI.

## Settings

In `BepInEx\config\mdbailey94.wol.damagemeter.cfg`:

| Setting | Default | |
|---------|---------|-|
| `Enabled` | true | Turn the whole mod on/off (also in the title screen Mods menu) |
| `Toggle` | F3 | Show/hide the panel |
| `Visible` | true | Panel shown (remembers your last F3 toggle) |
| `IdleSeconds` | 3 | Seconds without a hit that end a session (0.5-30) |
| `HoldSeconds` | 5 | Seconds the final numbers stay up before fading (0-60) |
| `MeterEnemies` | false | Also meter damage to real enemies, not just dummies |
| `Scale` | 1.0 | Panel size, 0.5 to 2 |

## How it works

The mod listens to the game's own "damage actually dealt" event, which fires once per hit after
crits, armor, elemental bonuses and every other modifier have been applied. The game caps that
number at the target's remaining health; since dummies can't die, the meter reads the uncapped
value for them so a dummy near 0 health still counts full hits. No Harmony patches are needed.

DPS runs from the first hit to the last, so the idle gap that ends a session isn't counted.
Sessions shorter than a second are divided by one second. Time is game time, so pausing (and the
game's brief slow-motion on big hits) doesn't count towards a session.

## Building it yourself

Same as [Rolling Gale](../WoLRollingGale/README.md#building-it-yourself).
