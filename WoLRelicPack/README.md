# Relic Pack (Wizard of Legend)

Four new **relics**, added with [LegendAPI](https://github.com/yekoc/LegendAPI). Each is an ordinary
game relic built on the game's own events and stat mods, so it stacks with other relics, shows
on the relic bar and comes off cleanly when dropped.

| Relic | Tier | Price | What it does |
| --- | --- | --- | --- |
| **Storm Battery** | 3 | 150 gold / 22 platinum | Dashing charges the battery, up to three times! Your next arcana hit unleashes chain lightning, one bolt per charge! |
| **Echo Charm** | 3 | 150 gold / 22 platinum | Your signature arcana echoes! When it ends, it is cast once more on its own at half power, with no cooldown! |
| **Glass Prism** | 2 | 150 gold / 18 platinum | Greatly increases critical hit chance, but reduces max health! |
| **Pickpocket's Glove** | 2 | 125 gold / 17 platinum | Gold drops are greater while your health is at half or below! |

### Storm Battery

- Every dash stores a charge, up to **3** (the relic bar lights up; full at 3).
- The next time one of your **arcana** (not your basic or dash arcana) hits an enemy, all the
  charges go at once: a chain of **lightning bolts**, one per charge, jumps from that enemy to the
  nearest other enemy within about 5 tiles, then the next, each enemy once. With nobody new in
  reach (a lone enemy or a boss), a bolt strikes the same enemy again, so no charge is wasted.
- Each bolt: **8 damage** (24 from a full battery; the game's Lightning Aura strikes for 10, but
  that's an arcana), a 50% chance to shock, a light stagger and no knockback. It can crit with
  your arcana's crit chance. (0.1.1 trimmed the bolts from 10.)

### Echo Charm

- Cast your **signature arcana** and, the moment the cast ends, your wizard casts it **once more
  by itself** (the echo) at **50%** damage. The echo is the real arcana: it aims where you're
  aiming at that moment, plays out like a normal cast and can be dash-cancelled like one. It costs
  nothing: **no cooldown** is added for it, it never spends your overdrive (it's always the plain
  arcana, even when the cast it echoes was the overdrive version), and it never echoes itself.
  (0.2.0: was an echo of each hit at 30%.)
- If you cancel the signature into a dash or basic, the echo waits for that (and anything you
  chain it into) to finish, for up to 2 seconds. It doesn't fire if you're hurt, frozen, falling,
  dead, parrying, paused, or a level is loading.
- **Charge arcana** (the ones with several stored uses that recharge one at a time, like Vacuum
  Shot and Ice Wave) keep the old behaviour instead, since their stored-charge bookkeeping lives
  inside the arcana and a free recast can't be made clean for them: for
  2.5 seconds after the cast, every enemy it hits is struck again 0.5 seconds later by an echo of
  that hit at 50% of its damage (status chances too), up to 12 echoes per cast.

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

`BepInEx\LogOutput.log` shows `Relic Pack 0.2.0 registered 4 relics`, then a line each time a
relic does something, for example:

- `Storm Battery: dash stored a charge (2/3)` and `Storm Battery: chain lightning landed 3 of 3 bolt(s) on 2 enemy(ies)`
- `Echo Charm: echoing UseX at 50% power` and `Echo Charm: echo of UseX ended` (for a charge
  arcana: `Echo Charm: UseX is a charge arcana, so its hits are echoed at 50% instead of recasting it`
  and `Echo Charm: echoed UseX on ... for 10 (50% of 20)`). The quickest check is the house's
  training dummies with the Damage Meter overlay on: the arcana fires twice, the second time for
  half the numbers, and the cooldown ring fills only once.
- `Glass Prism on for player 1: crit chance +25%, max health 500 -> 375`
- `Pickpocket's Glove active for player 1 at 48% health; gold drops now x1.50`

## Install

Needs BepInExPack of Legend (which includes Harmony), **LegendAPI** and **HookGenPatcher**. Download `WoLRelicPack.zip` from
the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it into
your Wizard of Legend folder.
