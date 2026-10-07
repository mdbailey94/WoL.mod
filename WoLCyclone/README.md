# Cyclone (Wizard of Legend)

A new **Air standard arcana**, added with [LegendAPI](https://github.com/yekoc/LegendAPI).

> **Cyclone**: Hold to whip up a twister you can steer, growing into a raging hurricane that
> bursts, blowing everyone away!
>
> *Enhanced: Reaches full strength sooner and bursts wider!*

- **Hold** the button: a tiny twister (the Twister arcana's own) spins up about **three tiles in
  front** of you while you stay planted, channeling. **Steer it** by pushing a direction (it stays put
  when you don't): quick while it's small, heavier as it grows, and barely moving from halfway on;
  never through walls, and up to 9 away from you.
- Over **4 seconds** it grows into a **hurricane**: the twister swells, the air boss's storm
  vortex (dust, debris, light streaks) builds up around it, layered swirls widen, dust streams in,
  debris and leaves whirl round it, and a rumble grows into a shake with gusts bursting out. As it grows it hits **faster** (every
  0.45 s down to 0.12 s) and **harder** (2, 3, 4, then 6 damage), tugging enemies inward.
- **Let go** early and it dies down. **Hold on one more second** at full power and it **bursts**:
  14 damage and a huge knockback that throws everyone around it away.
- **Enhanced**: full size in 3 seconds (bursting at 4), and a wider burst.
- Dash to cancel at any time. Cooldown 5 seconds.

Everything you see is the game's own wind effects, stripped down to just their looks (no scripts,
so nothing of the originals can hurt anyone or vanish early) and kept going for as long as you hold;
the hits come from invisible wind bursts; the log says `Cyclone: using the game's twister` and
`... storm vortex`, or which one it couldn't use.

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLCyclone.zip` from the
[latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it into your
Wizard of Legend folder. It shows up in the arcana shop (or straight away with Unlock Mod Content on).
