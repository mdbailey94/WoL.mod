# Relic Pack (Wizard of Legend)

Four new **relics**, added with [LegendAPI](https://github.com/yekoc/LegendAPI). Each is an ordinary
game relic built on the game's own events and stat mods, so it stacks with other relics, shows
on the relic bar and comes off cleanly when dropped.

| Relic | Tier | Price | What it does |
| --- | --- | --- | --- |
| **Storm Battery** | 3 | 150 gold / 22 platinum | Dashing charges the battery, up to three times! Your next arcana hit unleashes chain lightning, one bolt per charge! |
| **Echo Charm** | 3 | 150 gold / 22 platinum | Your signature arcana echoes! Everything it hits is struck again a moment later at reduced power! |
| **Glass Prism** | 2 | 150 gold / 18 platinum | Greatly increases critical hit chance, but reduces max health! |
| **Pickpocket's Glove** | 2 | 125 gold / 17 platinum | Gold drops are greater while your health is at half or below! |

### Storm Battery

- Every dash stores a charge, up to **3** (the relic bar lights up; full at 3).
- The next time one of your **arcana** (not your basic or dash arcana) hits an enemy, all the
  charges go at once: a chain of **lightning bolts**, one per charge, jumps from that enemy to the
  nearest other enemy within about 5 tiles, then the next, each enemy once. With nobody new in
  reach (a lone enemy or a boss), a bolt strikes the same enemy again, so no charge is wasted.
- Each bolt: **10 damage** (the game's Lightning Aura strikes for 10), a 50% chance to shock, a
  light stagger and no knockback. It can crit with your arcana's crit chance.

### Echo Charm

- Cast your **signature arcana** and, for the next 2.5 seconds, every enemy it hits is struck
  again **0.5 seconds** later by an echo of that hit at **40%** of its damage (status effect
  chances too), with a flash of the arcana's element on the enemy. Up to 12 echoes per cast.
- It echoes the hits rather than casting the arcana a second time: a real second cast would take
  over your wizard (aim, movement, animation, cooldown, overdrive), cut off an arcana that's still
  running and behave differently for every arcana. Echoing each hit works the same for all of
  them and lands exactly where the cast did.

### Glass Prism

- **+25% critical hit chance** on all your arcana (the Analytical Monocle gives 8%), and
  **-25% max health**. Both are the game's own stat mods, like Jeremiah's Needle and the cursed
  max-health relics, and come off when the relic does.

### Pickpocket's Glove

- While your health is at **50% or below**, **+50% gold** from every gold drop (enemies, and
  chests and pots that drop gold), through the same gold modifier the Midas Glove uses (+15%).
- In co-op the bonus is on while any wizard holding a glove is low, and it never doubles up.

## Settings

`BepInEx\config\mdbailey94.wol.relicpack.cfg` (made the first time you start the game with the mod):

| Section | Setting | Default | What it does |
| --- | --- | --- | --- |
| General | `Enabled` | true | Offer the relics in shops and drops (also in the title screen **Mods** menu). Off doesn't take them away mid-run. |
| Testing | `GiveRelicsKey` | (none) | A key (e.g. `F8`) that gives player 1 all four relics, for trying them out. |

The numbers above are constants at the top of each relic's file (`StormBattery.cs`,
`EchoCharm.cs`, `GlassPrism.cs`, `PickpocketsGlove.cs`); tiers and prices are at the top of
`RelicPackPlugin.cs`.

## Getting them

They're new relics, so they start **locked**: the plaza relic shop (platinum) can offer them.
With **Unlock Mod Content** on they're unlocked instead, and turn up in relic shops and chests
during runs. Or set `GiveRelicsKey` and press it once you're in a level (the plaza works).

## Checking it works

`BepInEx\LogOutput.log` shows `Relic Pack 0.1.0 registered 4 relics`, then a line each time a
relic does something, for example:

- `Storm Battery: dash stored a charge (2/3)` and `Storm Battery: chain lightning landed 3 of 3 bolt(s) on 2 enemy(ies)`
- `Echo Charm: signature UseX cast, echoing its hits for 2.5s` and `Echo Charm: echoed UseX on ... for 8 (40% of 20)`
- `Glass Prism on for player 1: crit chance +25%, max health 500 -> 375`
- `Pickpocket's Glove active for player 1 at 48% health; gold drops now x1.50`

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLRelicPack.zip` from
the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it into
your Wizard of Legend folder.
