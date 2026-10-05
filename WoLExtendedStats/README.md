# Extended Stats (Wizard of Legend)

Shows the numbers the game keeps hidden, inside the game's own menus, and tracks a run's worth of
extra stats for an end-of-run report. Works in co-op, with stats kept separately for each player.

## In the character menu (Select on controller)

The game's info box gets an extra section under the normal description:

- **Arcana highlighted:** that arcana's damage, crit chance and crit damage, and cooldown, all including
  relic and outfit bonuses. Once you've used it, this run's damage, hits and crits with it are shown too.
- **Cloak highlighted:** your wizard stats: health and shield, armor, evade, damage taken, healing,
  move and dash speed, gold and platinum.

If the extra text doesn't fit, the info box shrinks its font to keep it inside the frame.

## Post-run stats

When a run ends, by death or victory, a panel appears next to the end-of-run screen with:

| Stat | |
|------|-|
| Damage dealt, hits landed | Hits on enemies |
| Critical hits | Count and % of hits |
| Biggest crit / biggest hit | With the arcana that did it |
| Enemies defeated | Credited to whoever hit the enemy last |
| Knocked into pits | Enemies that fell after you last hit them |
| Damage taken, attacks evaded | |
| Dashes, top speed, distance travelled | |
| Top arcana | Your four most damaging arcana this run |

In co-op, player 1's panel is on the left and player 2's on the right.

## Full overlay (F2)

Press **F2** to pin everything (wizard, every arcana, and this run so far) on screen. Press F2 again to unpin.

## Install

Download `WoLExtendedStats.zip` from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest)
and unzip it into your Wizard of Legend folder. Only BepInEx is needed, not LegendAPI.

## Settings

In `BepInEx\config\mdbailey94.wol.extendedstats.cfg`:

| Setting | Default | |
|---------|---------|-|
| `Toggle` | F2 | Pin/unpin the full overlay |
| `Visible` | false | Overlay pinned (remembers your last F2 toggle) |
| `ShowInCharacterMenu` | true | Add stats to the character menu's info box |
| `ShowRunSummary` | true | Show the post-run stats panel |
| `Scale` | 1.0 | Overlay/summary panel size, 0.5 to 2 |

## Building it yourself

Same as [Rolling Gale](../WoLRollingGale/README.md#building-it-yourself), plus `UnityEngine.UI.dll`
(from `WizardOfLegend_Data\Managed`) and `0Harmony.dll` (from `BepInEx\core`) in `lib`.
