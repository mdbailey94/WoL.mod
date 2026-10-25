# Tidal Wall (Wizard of Legend)

A new **Water standard arcana**, added with [LegendAPI](https://github.com/yekoc/LegendAPI). It has
a **signature version**, so it can go in the signature slot.

> **Tidal Wall**: Raise a wall of water that surges forward, washing enemies away and sometimes
> freezing them!
>
> *Enhanced: The wave is wider and surges further!*
>
> *Signature: A tidal wave that rolls on twice as far and freezes everything it catches!*

| Stat | Value |
|------|-------|
| Element | Water |
| Tier | 2 (like Tidal Blast) |
| Price | 6 (like Tidal Blast) |
| Damage | 8 per row, 20 for the crest (up to 44 per enemy carried the whole way; 52 enhanced). 0.1.1 trimmed the rows from 10 (50 in all): a wave that shoves the whole way and can freeze stays under Tidal Blast's 65 |
| Knockback | 30 per row, 50 for the crest, always straight along your aim |
| Freeze | 15% chance per row, 50% for the crest, 1.5 s |
| Cooldown | 6 s |
| Wave | 4 rows, 3 wide (5 rows, 5 wide when enhanced) |
| Signature | 5 wide and twice as many rows (8; 10 when also enhanced), rows a little larger, 12 per row and 40 for the crest (up to 124; 148 when also enhanced), knockback 35 / 60, **every hit freezes** for 2 s |

A wall of water rises in front of you and rolls forward: each row of water swells up, then bursts
a tenth of a second later as the next row rises a step further out, so the wave keeps shoving
whatever it catches the same way, straight along your aim. The last row is a bigger **crest** that
hits harder and throws enemies further. The water is the game's own Tidal Blast water (the orbs
and their deluge burst), so it looks and sounds like a native Water arcana.

Freeze is the game's Water status effect (Air's is Slow). Plain water arcana like Tidal Blast
don't freeze at all and ice ones always do, so Tidal Wall has a chance. A lane of the wave that
runs into a wall stops there instead of passing through it. Dash to cancel; any water that hasn't
burst yet drains away.

## Signature

Put Tidal Wall in the **signature slot** (the spellbook in your house lists it under Signature
Arcana) and cast it with the signature gauge full: the wall becomes a **tidal wave**. It's 5 wide
like the enhanced wall and rolls on **twice as far** (8 rows, or 10 when the arcana is also
enhanced, which it always is in the signature slot), the rows and the crest are a little larger,
they hit harder (12 a row, 40 for the crest) and **every row freezes** what it hits, for 2 seconds.
As with the game's own arcana the cast spends the whole gauge and its hits don't refill it.

The signature version is **unlocked along with the arcana** (buy it once and it's in the Signature
list). If you'd rather earn it the vanilla way, finding its Signature drop in a run, turn
`SignatureUnlocksWithArcana` off in `BepInEx\config\mdbailey94.wol.tidalwall.cfg`.

## Checking it works

`BepInEx\LogOutput.log` shows `Tidal Wall 0.2.0 registered` at start-up, then for each cast:

```
Tidal Wall: cast (normal), 4 rows x 3 wide toward (1.00, 0.00)
Tidal Wall: using the game's Tidal Blast water orbs (hit area ... )   <- first cast only
Tidal Wall: wave done, 12 bursts, 5 hits on 2 targets, 1 frozen
```

The first line says which version ran: `normal`, `enhanced`, `signature` or
`both: enhanced + signature`. A signature cast ends with `Tidal Wall: signature wave done, ...`.
When the signature is unlocked you get `Tidal Wall: signature unlocked along with the arcana ...`
once. The gauge fills by dealing damage; the training dummies in your house work for that.

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLTidalWall.zip` from
the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it into
your Wizard of Legend folder. It shows up in the arcana shop in the Plaza (or straight away with
Unlock Mod Content on).

## Tuning

Damage, knockback, freeze chance and cooldown are in `TidalWallPlugin.cs` (`skillStats`; levels 1
and 2 are the wall's rows and crest, 3 and 4 the signature wave's). Rows, width, spacing, timing and
sizes are the constants at the top of `TidalWallState.cs`.

## Building it yourself

Put `Assembly-CSharp.dll`, `UnityEngine.dll` (from `WizardOfLegend_Data\Managed`), `BepInEx.dll`
and `0Harmony.dll` (from `BepInEx\core`) and `LegendApi.dll` into a `lib` folder at the repo root,
then run `dotnet build -c Release`.
