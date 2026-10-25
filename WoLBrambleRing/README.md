# Bramble Ring (Wizard of Legend)

A new **Earth standard arcana**, added with [LegendAPI](https://github.com/yekoc/LegendAPI). It has
a **signature version**, so it can go in the signature slot.

> **Bramble Ring**: Knit a thicket of thorny vines around you that snares the enemies inside and
> pricks them while it stands!
>
> *Enhanced: The thicket stands longer and spreads outward into a wider ring!*
>
> *Signature: The wide thicket erupts from the ground, stands even longer, its vines tighten,
> dragging snared enemies to the centre, and at the end it snaps shut and crushes them!*
>
> (In game the enhanced and signature versions share one line: *The thicket stands longer and
> spreads wider! Signature: it erupts, drags foes inward, then crushes them!*)

| Stat | Value |
|------|-------|
| Element | Earth |
| Tier | 4 (like Rippling Vines and Terra Ring) |
| Price | 6 (like Rippling Vines and Terra Ring) |
| Damage | 5 for the catch, then 3 a prick every 0.5 s: 20 over the full 3 s (enhanced 29 over 4.5 s). 0.2.1 trimmed the pricks from 5 (30 a full stand): a hard snare for the whole stand shouldn't also out-damage Embracing Vines (25) |
| Control | Snare: the game's root (held in place, no knockback), renewed by every prick, 1 s past the last. Bosses are slowed instead (50% for 2 s) |
| Cooldown | 6 s |
| Reach | about 2.4 units around where it was cast (enhanced: 3.4). 0.4.1 drew it in from 3 (4): it was too big |
| Signature | All three rows erupt outward (reach 3.4), stands 6 s, 10 for the catch and 6 a prick, every half second each snared enemy is dragged 0.5 units toward the centre, and a closing crush of 12 on everything inside (88 for an enemy held the whole way) |

Your wizard slams the ground (Rippling Vines' slam) and thorny vines knit a thicket around them,
one after another round the ring, in two rows about 1.1 and 1.9 units out. The vines are the
game's own vine traps, the ones Rippling Vines and the Root Shield relic raise, each with its own
grabbing hit box. The thicket stands where it was cast for **3 seconds** while you walk off (or
stay inside it, nothing in it hurts you).

Every half second, every enemy inside the thicket is **pricked** by the thorns (5 for the first
prick, 3 for each after it, leaves fly) and **snared** with the game's root status: held where it stands, ignoring knockback, until a
second after its last prick. Enemies that walk into the thicket are caught the same way, and a vine
whose hit box they cross grabs them and holds them on the spot as well. The first prick on an enemy
(the catch) makes it flinch and also slows it; the pricks after that don't flinch, so a snared enemy
can still fight back from inside. Bosses, and anything else the game won't let a grab hold, aren't
snared: they get the catch (and its slow) on every prick instead.

**Enhanced**, the thicket stands **4.5 seconds**, and a moment after the first two rows a third,
wider row of vines spreads out about 2.9 units from the centre, so everything within 3.4 units is
pricked.

## Signature

Put Bramble Ring in the **signature slot** (the spellbook in your house lists it under Signature
Arcana) and cast it with the signature gauge full: the thicket comes up at its **full, enhanced
size** (all three rows, everything within 4 units), stands **6 seconds** (the catch and 11
pricks), hits harder (10 for the catch, 6 a prick) and the **vines tighten**: every half second each
snared enemy is **dragged half a unit toward the centre** (its body is moved the way Mortal Coil
drags the enemies it hooks, since the snare itself blocks knockback), never into a wall, until it's
at the middle of the ring. Bosses and anything else that can't be snared aren't dragged either. When
it's done, the thicket **snaps shut and crushes** everything still inside it for **12** (bosses
too). The signature's size and time take over when the arcana is also enhanced (which it always is
in the signature slot). As with the game's own arcana the cast spends the whole gauge and its hits
don't refill it.

The signature is played as a set piece, with the game's own camera, time, lighting and effects:

1. **Windup** (0.25 s): the wizard raises the slam from the very top (as Grasping Earth does) and
   holds it while the ground rumbles, dust and leaves stir and the camera leans in a little.
2. **Slam**: a hit-stop (6 frames), a heavy shake, a large crater with cracks, rocks and a burst of
   dust (the earth boss's landing and its explosion sound), a camera punch-in, a flash of the room's
   light, then a short slow-down (0.4 s easing back to full speed) as the room sinks into a
   **green-tinted gloom** that lasts as long as the thicket.
3. **Eruption**: the three rows burst up one after another from the centre outward (0.08 s apart,
   stretched by the slow-down), every vine throwing up rocks, leaves and an earth wave, every row its
   own ring of dust and a shake that weakens as the wave spreads.
4. **Constrict**: each half second that the vines drag anyone, leaves burst off the dragged enemies,
   the vines creak and the screen gives a small shake.
5. **Final snap**: every vine whips shut, and a moment later the crush lands with a hit-stop
   (7 frames), a shake, a punch-in, a flash and a burst of thorns, leaves and rocks. Then the vines
   wither and the light comes back.

Everything the cinematics change is put back: the camera zoom only lasts while it's driven (and is
set back to the game's own size when it ends, or at once if the cast is interrupted, the wizard
dies, a cutscene takes the camera or the level changes); the hit-stops and slow-downs are the game's
own, which restore the time scale themselves (checked afterwards, and forced back if stuck); and the
room's light is faded back to exactly what it was when the thicket withers, at once on a level load,
early if the wizard dies or the thicket's timer stops, and left alone if the game changes the light
itself meanwhile (a boss room event). A signature thicket left behind by a level load ends without
its snap. No zoom in PvP, where the game sizes the camera itself.

The signature version is **unlocked along with the arcana** (buy it once and it's in the Signature
list). If you'd rather earn it the vanilla way, finding its Signature drop in a run, turn
`SignatureUnlocksWithArcana` off in `BepInEx\config\mdbailey94.wol.bramblering.cfg`.

## Log

`BepInEx\LogOutput.log` shows:

- `Bramble Ring 0.4.0 registered` at start-up.
- `Bramble Ring: cast (normal) at (x, y)` each cast; the version that ran is `normal`, `enhanced`,
  `signature` or `both: enhanced + signature`.
- `Bramble Ring: knitted 18 vines in 2 rows 1.5-2.5 out` once the rows are up, with how many spots
  were skipped in walls or pits; enhanced, `Bramble Ring: vines spread outward: 16 more at 3.5 out`.
- A signature cast's beats, in order:
  - `Bramble Ring: signature: windup, holding the slam 0.25 s`
  - `Bramble Ring: signature: the slam lands, hit-stop 6 frames into a 0.4 s slow-down, shake,
    camera punch-in to 0.88, the earth cracks`
  - `Bramble Ring: signature: gloom on, room light 0.70 dimming to 0.39 and tinting green` (or
    `signature: no gloom, this room has no stage light`)
  - `Bramble Ring: signature: vines erupted outward in 3 rows 1.5-3.5 out, 0.16 s centre to edge:
    34 vines`
  - `Bramble Ring: signature: constrict pulse 3, dragged 2 inward` on each tick that dragged anyone
  - `Bramble Ring: signature: final snap, 34 vines whipped shut, crushed 3 enemies for 36 damage
    before armour`
  - `Bramble Ring: signature: gloom off: room light restored to 0.70`
  - `Bramble Ring: signature: light, camera and time all restored (light back to 0.70; camera size
    back to 9.00; time scale 1.00)` once everything is back.
  - If it goes off script: `signature: cast interrupted before the slam` (and `camera easing back`),
    `signature: gloom lifting early, the wizard is down`, `signature: thicket left behind by a level
    load, ...`, `signature: gloom off (level load): ...`, `signature: gloom off: the game changed the
    room's light itself ...`, `signature: camera zoom ended early, ...`, `signature: time scale was
    left at ..., forced back to 1.00`, `signature: no camera zoom, ...`.
- `Bramble Ring: caught EarthArcher(Clone), snared` for each enemy the first time it's pricked
  (`in a vine` when a vine grabbed it first). Instead of `snared` you may see
  `slowed (can't be snared)` (bosses), `hit only` (it resisted the status) or `killed`.
- `Bramble Ring: thicket withered after 3.0 s: caught 3 (...), 1 of them by a vine first (2 vine
  hits in all), 15 pricks for 51 damage before armour` (or `caught nothing`) when it's gone. A
  signature cast says `signature thicket withered after 6.0 s: ...` and ends with `, dragged them
  toward the centre 20 times (8.5 u in all, 7 constrict pulses)`.
- `Bramble Ring: signature unlocked along with the arcana ...` once, when the signature is unlocked.
- `Bramble Ring: vine hit box is a ...` once, the size and layer of one vine's hit box.
- `Bramble Ring: effect '...' unavailable, skipping it` if a cosmetic effect can't be made.

The gauge fills by dealing damage; the training dummies in your house work for that.

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLBrambleRing.zip`
from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it
into your Wizard of Legend folder. It shows up in the arcana shop (or straight away with Unlock
Mod Content on).

## Tuning

Damage, cooldown, the snare and the slow are in `BrambleRingPlugin.cs` (`skillStats`; levels 1 and
2 are the catch and the pricks, 3 and 4 the signature thicket's, 5 its final crush). The ring sizes,
how long it stands, the prick interval, the signature pull, the knitting speed and every signature
beat (windup, hit-stops, slow-downs, shakes, zooms, eruption timing, effect counts) are the
constants at the top of `BrambleRingState.cs`; the gloom's depth, tint and fades are at the top of
`BrambleCinematics.cs`; how the vines are raised is in `BrambleVines.cs`.

## Building it yourself

Put `Assembly-CSharp.dll`, `UnityEngine.dll` (from `WizardOfLegend_Data\Managed`), `BepInEx.dll`
and `0Harmony.dll` (from `BepInEx\core`) and `LegendApi.dll` into a `lib` folder at the repo root,
then run `dotnet build -c Release`.
