# Slingshot and Blazing Slingshot (Wizard of Legend)

Two new **dash** arcana, added with [LegendAPI](https://github.com/yekoc/LegendAPI). Both charge
the same way; they differ in what the launch does.

### Charging (both)

- **Tap** dash: a normal dash.
- **Hold** dash: your wizard hops backward, like pulling back a slingshot, then holds that pose
  while dust builds at their feet (up to 1 second). Keep aiming to turn and line up the shot.
- **Release**: launch the way you're aiming. A full charge gives **+60% dash speed and +40% dash
  time** (about 2.2× the distance). Holding the full second launches you automatically.

### Slingshot (Air)

> **Slingshot**: Hold to pull back and charge, then release to launch yourself across the room,
> blasting enemies away where you leave and land!

Wind bursts go off **where you leave and where you land**, knocking nearby enemies hard away.
Both get bigger with more charge.

### Blazing Slingshot (Fire)

> **Blazing Slingshot**: Hold to pull back and charge, then release to drive a long flaming punch
> across the room, sucking enemies in behind you!

- A **long flaming punch** rides out in front of you for the whole dash. Enemies it hits are
  thrown **backward, behind you**.
- You leave a **trail of flame bursts** that pull nearby enemies into your path.
- Where you land, a bigger flame burst **pulls everyone nearby into a pile**.
- Everything scales with charge, and it's Fire, so it can burn.

You're not invulnerable while charging, so time it. Careful hopping backward near a ledge.

## Tuning the pose

The hop and the held pose reuse the game's own wizard animations. In
`BepInEx\config\mdbailey94.wol.slingshotdash.cfg`, under `[Charge]`:

- `Animation`: `Jump` (default), `Charge`, `Slide`, `Hurt`, `Kick`, `Parry`, `Slam`, `Fall` or `None`.
- `PoseFrame`: which moment of that animation is held, from `0` (start) to `1` (end). Default `0.4`.
- `HopDistance`: how far the hop goes. Default `1.5`; `0` turns the hop off.

Changes apply the next time the game starts.

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLSlingshotDash.zip`
from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it
into your Wizard of Legend folder. Find them in the arcana shop, or install **Unlock Mod Content** to
have them unlocked right away.

## Troubleshooting

If holding dash doesn't charge, check `BepInEx\LogOutput.log` for the lines starting with
`Dash button` and send them over.
