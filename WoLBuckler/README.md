# Buckler

A new **Earth basic arcana** (#87, part of the giant weapon set). It's a small stone shield worn for punching. The combo is **hit, hold, hit, hold, toss**:

| Hit | Default damage | Behaviour |
|---|---:|---|
| 1-2: punch, then hold | 7 | Earth burst 1.0 u in front of your hands (0.75 u radius), knockback 6. The buckler is then held up in front for 0.55 s, turned to your aim, and breaks enemy shots that hit its face (within 0.8 u). |
| 3: disc toss | 10 out, 10 back | The buckler spins out 5.5 u at 14 u/s and back to your hands at 16 u/s. It hits each enemy once each way and breaks every enemy shot it meets. |
| Enhanced shrapnel | 5 | Each shot the buckler breaks bursts into rocks (0.8 u). |

**Enhanced** gives you the big, swing-sized buckler: everything 1.75x bigger (the buckler, its hits, its guard and the disc). Knockback rises to 30 on punches and 45 on the disc, with screen shake, and broken shots burst into shrapnel.

Timing is deliberate: a 0.2 s wind-up on each punch (0.24 s on the toss) and a longer pause between hits than the game's basics. Shots are broken with the same checks as Stone Charge's shield; a boomerang is sent back instead.

Art: WoL Art's buckler, 4-frame punch and icon ('Giant weapons (2026-11-03)'). All numbers are in `BepInEx\config\mdbailey94.wol.buckler.cfg`; an older config is moved onto these numbers automatically. Requires LegendAPI.

**0.5.0** (#94): WoL Art's held buckler in 8 facings (buckler_held_8dir, 16x16): after the punch's smash the raised buckler shows the frame for your aim (right, the diagonals, down, left, up), upright, and goes behind the wizard for the three up facings. The thrown disc plays Art's spin strip at 24 fps instead of turning the buckler (buckler_disc; the enhanced disc uses the native 26 px buckler_disc_big). Same sizes, hits and timing. `[Look] DirectionalArt = false` brings back the 0.4.0 look.

**0.6.0** (#94, Log - Art 09:05): WoL Art's punch in 8 directions (buckler_punch_8dir, 8 facings x 4 frames of 40x40, drawn from the shoulder): the arm pulls back during the wind-up, smashes, holds and recovers in the direction you punch, with the buckler strapped to the fist, then the 8-facing held buckler stays up through the pause. The up facings are drawn behind the wizard. Hits, guard and timing are unchanged; `[Look] DirectionalArt = false` still gives the 0.4.0 look.

