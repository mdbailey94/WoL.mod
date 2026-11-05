# Plan: item tiers and bonuses (Chaos Depths)

Asked for by Matt on 2026-10-28: "tiers of stats and bonus, even to the legendarys". This is a
plan, not built yet.

## How items roll today

- **Rarity** sets how many affixes an item has and how strong they are:
  - Common: 1 affix at ×0.6
  - Magic: 2 affixes at ×1
  - Rare: 3 affixes at ×1.25
  - Legendary: 4 affixes at ×1.6
- **Each affix value** is picked evenly between its min and max, plus a little per item level.
- **Uniques** are Legendaries that also have a power (Chain, Echo, Pyre, Gale, Leech, Thorn).
  The power's strength is one fixed config value, the same on every copy.
- So two items of the same rarity and level differ only in which affixes they got and where
  each value landed. Nothing marks a great roll, and nothing beyond affixes and the unique power.

## Proposal

### 1. Item tier, T1 to T5, on every item (Legendaries and uniques included)

Each drop rolls a tier separately from its rarity. A Common can be a T5 Common and a Legendary
can be a T1 Legendary. The tier scales every affix value and decides how many bonus lines the
item gets:

| Tier | Affix values | Bonus lines | Weight at A0 |
|------|--------------|-------------|--------------|
| T1   | ×0.85        | 0           | 30           |
| T2   | ×1.00        | 0           | 40           |
| T3   | ×1.15        | 1           | 22           |
| T4   | ×1.30        | 1           | 7 (from A1)  |
| T5   | ×1.50        | 2           | 1 (from A3)  |

- **Ascension 0** stops at T3, so the base game still leads (Matt's rule for A0). Each
  Ascension level and each 5 depths shift weight toward the top tiers. Elites and bosses get a
  tier boost, the way they already get a rarity boost.
- **Shown** as pips or "T4" on the inventory row and in the VS comparison panel, and as a
  thicker item frame for T4–T5 (an art task).
- **Scoring:** the tier counts in auto-equip and in the VS comparison.
- **Salvage:** dust is multiplied by the tier.

### 2. Bonus lines: a second pool, separate from affixes

These are effects rather than plain stat percentages, so a bonus reads differently from an
affix. Examples that fit Wizard of Legend:

- +X% damage with one element's arcana (Fire, Water, Earth, Air, Lightning, Chaos).
- A dash leaves a small burst, and a T5 one leaves two.
- Signature charge rate +X%.
- +X gold or Chaos Dust per kill.
- The first hit on an elite deals +X%.
- Standing still for 1 s gives +X% damage taken reduction.

Each bonus has a value band, and the item's tier picks where in the band it rolls.

### 3. Tiers on Legendaries and uniques

- Legendaries roll a tier like everything else, so a T5 Legendary is the chase item.
- **Unique powers scale with tier.** The power's config value becomes the T2 value and the
  other tiers scale from it. For example, the Stormlinked Signet's arc would be
  30/35/40/45/52% at T1–T5, and the Bloodwell Charm's leech 1.5/2/2.5/3/3.5%.
- A **T5 unique** also gets its two bonus lines, so it is the best item in the game.

### 4. Data and save

- `loot.json` gains a `tiers` list: name, weight, value scale, bonus lines, the minimum
  Ascension each tier can drop at, and a weight shift per Ascension level. A `bonuses` pool sits
  beside `affixes`, so balance stays editable without a rebuild.
- `ItemData` gains `tier` and `bonuses`. Items already in a save load as T2, which leaves their
  values exactly as they are now.

## Build order

1. Tier roll, value scaling, save field, and tier shown in the inventory and VS panel. Small and
   testable alone.
2. The bonus-line pool with 4–6 bonuses, each hooked into the existing hit, kill, and hurt
   events. Uniques.cs already has those hooks.
3. Unique power strength by tier.
4. Balance pass with WoL Player, plus art for the tier frame and pips (Art).

## Questions for Matt

- One tier for the whole item (this plan, easier to read at a glance) or a tier per affix
  (Diablo-style: more to read, finer rolls)?
- Five tiers, or three (Lesser / Normal / Greater)?
- Should A0 have tiers at all, or start them at Ascension 1 like the other new systems?
