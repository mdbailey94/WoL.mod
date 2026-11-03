# Skybreaker (Wizard of Legend)

A new **Lightning standard arcana** (Matt's #52), added with [LegendAPI](https://github.com/yekoc/LegendAPI).

> **Skybreaker**: Call down a single mighty bolt of lightning on the spot ahead: devastating at its
> heart, crackling around it!
>
> *Enhanced: Strikes sooner and wider! Signature: the sky splits open!*

| | Standard | Enhanced | Signature |
|---|---|---|---|
| Where | 4 u ahead along your aim | same | same |
| Delay | 0.45 s | 0.3 s | 0.8 s of gathering storm |
| Core (always shocks) | 1 u across, 28 | 1.3 u, 32 | 2 u, 70 |
| Ring (30% shock) | 2 u across, 6 | 2.6 u, 6 | 4 u, 20 |
| One enemy in the core | 34 | 38 | 90 |

Tier 3, price 5, cooldown 4.5 s. Core and ring are separate bursts, so an enemy in the core takes
both; one at the edge only the ring.

**Cast** it with a quick forehand: sparks crackle over the spot, closing in as the bolt nears, then
the bolt strikes (the game's own lightning-strike animation, burst, scorch mark, thunder and a
shake). The wizard is free to move as soon as the cast is done.

**Signature**: the stage darkens and the camera closes in while lightning arcs into the spot, then a
colossal bolt lands with a hit-stop, a white flash and a moment of slow motion, and a ring of
aftershock bolts follows round its edge. Everything is put back afterwards (also if a level load
cuts it short).

All numbers are in `BepInEx\config\mdbailey94.wol.skybreaker.cfg` ([Bolt], [Enhanced], [Signature],
[Balance]). The icon is Thunderhead's for now.

**0.2.0** (Matt's #64): a target circle the ring's size pulses on the floor until the bolt lands; the bolt is one single large strike with nothing else round it (bolt drawn 2.2x); the signature is one very large bolt (4.5x) with a deeper thunder, no aftershocks ([Signature] Aftershocks = 0; a 0.1.0 config is moved over on first launch).
