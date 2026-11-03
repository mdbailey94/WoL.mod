# Buckler

A new **Earth basic arcana** (#87, part of the giant weapon set). It's a small stone shield worn for punching: two heavy, swing-sized punches from the hands, with no charge. The buckler turns to face each punch, and while it's out it breaks any enemy shot that meets its face, using the same checks as Stone Charge's shield; a boomerang is sent back instead.

| Hit | Default damage | Behaviour |
|---|---:|---|
| 1: punch | 7 | Earth burst 1.3 u in front, 1.1 u radius, knockback 25 |
| 2: last punch | 11 | Earth burst 1.3 u in front, 1.4 u radius, knockback 45 |
| Enhanced shrapnel | 5 | Enhanced: each shot the buckler breaks bursts into rocks (0.8 u) |

It's deliberate: a 0.2 s wind-up (0.26 s on the last punch) and a longer pause than the game's basics, with a step into each punch. The buckler is drawn about 1.75 u across, stays out for 0.45 s per punch, and breaks shots within 1.2 u of it.

Art: WoL Art's buckler, 4-frame punch and icon ('Giant weapons (2026-11-03)'). All numbers are in `BepInEx\config\mdbailey94.wol.buckler.cfg`. Requires LegendAPI.
