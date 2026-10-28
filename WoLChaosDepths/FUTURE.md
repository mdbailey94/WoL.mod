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

## Charm uniques for the red, green and violet orbs (draft for Matt's review)

Since 0.15.12 a plain Legendary charm wears the orange orb, and its theme shows only in the
trail. The three themed orbs belong to these uniques. Each is a single orb circling at waist
height, leaving its own coloured trail.

**Embersting Charm (red orb, offense).** Every 1.5 s the orb darts at the nearest enemy within
5 units and back again, hitting it for 8 damage plus 1 per character level (9 at level 1, 58 at
50). A small ember burst shows where it hits. It doesn't fire while you're in the Hub. Config:
`EmberstingEvery` 1.5, `EmberstingRange` 5, `EmberstingDamage` 8, `EmberstingPerLevel` 1.

**Mossguard Charm (green orb, defense).** Every 12 s the orb hardens into a ward: it grows a
pixel and gets a bright green rim. The next hit you take while it's up does 50% less damage.
The ward then breaks into green shards and starts charging again. Config: `MossguardEvery` 12,
`MossguardBlock` 0.5.

**Voidmote Charm (violet orb, utility).** Enemies within 3 units of you are slowed by 25%. A
violet mote hangs over each slowed enemy, and the slow ends as soon as they leave the range.
Bosses are slowed half as much. Config: `VoidmoteRadius` 3, `VoidmoteSlow` 0.25.

All three would join the unique pool. That makes 9 uniques, each about a ninth of the unique
drops. Their inventory icons would be the themed orb drawn at icon size until Art makes proper
ones. The first-boss testing unique could roll any of them.

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
