# Chaos Depths: future changes

Things Matt has asked to have later. Each one moves to the README's version notes when it's
built.

## Level-up effect

Asked for 2026-10-28. Right now a level-up shows only the text notice ("Level N") and a log
line, which is easy to miss now that levels come about a third as often (0.15.10). Ideas:

- A burst of light rising from the wizard's feet, pixel-drawn in the game's style like the
  unique powers' effects (PowerFx). It would play over the body for about half a second.
- "LEVEL UP" in the pixel font above the head, rising and fading, plus the health gained
  ("+2% health").
- One of the game's own sounds, such as the one for picking up a relic or a signature getting
  charged, so it fits the game.
- Art could draw the burst frames (`levelup_burst_0..N`) the way they drew the drop glows;
  until then the code draws them.
- In co-op it would play on the player who levelled, in that player's colour.

## Uniques for the nine themed Legendary looks (on hold)

Proposed 2026-10-28, and Matt put them on hold the same day. The offense (red), defense (green)
and utility (violet) looks of the ring, pendant and charm have their own art but no power.
Proposed, kept mild:

- Reaper's Band (red ring): +40% damage to enemies below 30% health.
- Wrathstone (red pendant): each kill gives +8% damage for 4 s, stacking to 5.
- Embersting Charm (red charm): the circling stones shoot the nearest enemy every 1.5 s.
- Warden's Band (green ring): 30% less damage taken for 2 s after being hit.
- Heartwood Amulet (green pendant): once per floor, a hit that would drop you below 25%
  health heals 20% instead.
- Mossguard Charm (green charm): after 5 s unhit, regenerate 1% max health every 2 s.
- Shadeband (violet ring): a kill doubles evasion for 2 s.
- Starwind Pendant (violet pendant): a kill cuts cooldowns 20% for 3 s.
- Voidmote Charm (violet charm): enemies near the circling stones are slowed 25%.

The trade-offs: 15 uniques instead of 6, so each is rarer, and plain Legendaries would lose
these looks.

## Item tiers and bonus lines

See `PLAN-tiers.md`. Matt wants changes to the plan before it's built and hasn't said which
yet.
