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

## Item tiers and bonus lines

See `PLAN-tiers.md`. Matt wants changes to the plan before it's built and hasn't said which
yet.
