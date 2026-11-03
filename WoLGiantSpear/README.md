# Giant Air Spear

A new **Air basic arcana** (#85, part of the giant weapon set). It's a huge spear of wind swung slowly: two long, heavy stabs along your aim, then a throw. The thrown spear flies out, piercing everything in its path, and blows away into wind at the end. Set `[Spear] ReturnToHand` to make it fly back and hit again, as 0.1 did.

| Hit | Default damage | Behaviour |
|---|---:|---|
| 1-2: stab | 12 | Wind burst on every enemy within 3.4 u along the aim (0.9 u wide), knockback 12, camera shake |
| 3: throw | 16 | Flies 6.5 u at 16 u/s, piercing, then blows away |
| Enhanced gust | 10 | Enhanced: a 1.6 u gust where the throw ends |

Enhanced also gives +25% stab reach and throw range. It's slow, as a giant weapon should be: a 0.38 s wind-up on each stab and 0.5 s on the throw, with a late cancel. Gear activation speed shortens all of it.

It's drawn as wind, in see-through cyan with fading wind ghosts and the game's own wind streaks, using WoL Art's spear at giant scale: the #44 thrust for the stabs, plus the giant thrown spear and icon ('Giant weapons (2026-11-03)'). All numbers are in `BepInEx\config\mdbailey94.wol.giantspear.cfg`. Requires LegendAPI.
