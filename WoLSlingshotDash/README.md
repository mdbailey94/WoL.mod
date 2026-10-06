# Slingshot, Blazing Slingshot and Frost Slingshot (Wizard of Legend)

Three new **dash** arcana, added with [LegendAPI](https://github.com/yekoc/LegendAPI). Both charge
the same way; they differ in what the launch does.

### Charging (all three)

- **Tap** dash: a normal dash.
- **Hold** dash: your wizard hops backward, like pulling back a slingshot, then holds that pose
  while dust builds at their feet. The charge is full after 1 second, but you can keep holding
  as long as you like. Keep aiming to turn and line up the shot.
- **Release**: launch the way you're aiming. A full charge gives **+60% dash speed and +40% dash
  time** (about 2.2× the distance).

### Slingshot (Air)

> **Slingshot**: Hold to pull back and charge, then release to launch yourself across the room,
> blasting enemies away where you leave and land!

Wind bursts go off **where you leave and where you land**, knocking nearby enemies away: hard at
the launch, gentler where you land. Both get bigger with more charge.

### Blazing Slingshot (Fire)

> **Blazing Slingshot**: Hold to pull back and charge, then release to blitz across the room in
> flames, leaving a vacuum that sucks enemies in!

- You rush forward wrapped in **Blazing Blitz's flame trail**, dropping flame bursts that pull
  nearby enemies into your path.
- Where you land, a **flame vacuum** keeps sucking everyone nearby in for about half a second.
- Everything scales with charge, and it's Fire, so it can burn.

### Frost Slingshot (Ice)

> **Frost Slingshot**: Hold to pull back and charge, then release to throw an ice feint and swap
> places with it, freezing enemies where you stood!

- Instead of dashing, you **throw an ice copy of yourself** (the game's ice decoy) along your aim:
  3 to 9 tiles depending on charge, stopping short of walls.
- When it lands you **swap places**: you appear where it landed, and it appears where you stood.
- It bursts with a **small Frost Nova** there, freezing enemies around it, then lingers for a
  couple of seconds so enemies keep going after it.

### Balance

Slingshot arcana trade a long cooldown and a charge-up for more range and damage than a normal
dash, without outclassing standard arcana. Each has **one charge** that recharges in **7 s** (see
`CooldownSeconds` below), and a tap uses the charge too, since it's the same dash.

| | Damage per enemy | Notes |
|---|---|---|
| Slingshot | 10 at launch, 6 where you land (up to 16) | Strong knockback at launch, gentle at landing |
| Blazing Slingshot | 4 per trail burst, 2 per vacuum pulse (about 20 at most) | Plus burn; pulls instead of pushing |
| Frost Slingshot | 12 from the Frost Nova | Freezes for 1.5 s; the feint draws enemies |

You're not invulnerable while charging, so time it. Careful hopping backward near a ledge.

## Settings

The hop and the held pose reuse the game's own wizard animations. In
`BepInEx\config\mdbailey94.wol.slingshotdash.cfg`, under `[Charge]`:

- `Animation`: `Jump` (default), `Charge`, `Slide`, `Hurt`, `Kick`, `Parry`, `Slam`, `Fall` or `None`.
- `PoseFrame`: which moment of that animation is held, from `0` (start) to `1` (end). Default `0.4`.
- `HopDistance`: how far the hop goes. Default `1.5`; `0` turns the hop off.
- `MaxHoldSeconds`: launch automatically after holding this long. Default `0`: hold as long as
  you like.

Under `[Balance]`:

- `CooldownSeconds`: cooldown of all three slingshots. Default `7`.

Changes apply the next time the game starts.

## Changing it in code

Anything not in the config file lives in the code:

- **Damage, knockback, freeze, element, shop price**: the `Skills.Register(...)` blocks in
  `SlingshotDashPlugin.cs`, one per arcana. Stat arrays have one entry per skill level; where
  there are two (e.g. `damage = new[] { 10, 6 }`), level 1 is the launch and level 2 the
  landing, trail or pulses, as the comments say.
- **Charging** (time to full charge, tap window, speed/duration bonus, hop timing): the
  constants at the top of `ChargedDashState.cs`.
- **Each arcana's attacks**: `SlingshotDashState.cs` (Air), `BlazingSlingshotState.cs` (Fire)
  and `FrostSlingshotState.cs` (Ice): burst sizes, intervals, throw distance and so on, as
  constants at the top or numbers in the calls.

To build your change, push it to a branch and open a pull request: GitHub Actions builds every
mod and, once merged, publishes the zips on the
[latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest). To build locally
instead, install the .NET SDK, put the reference DLLs listed in `.github/workflows/build.yml`
in a `lib` folder at the repo root, and run
`dotnet build WoLSlingshotDash/WoLSlingshotDash.csproj -c Release`; if your game is in the
default Steam folder, the DLL is copied into `BepInEx\plugins` automatically.

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLSlingshotDash.zip`
from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it
into your Wizard of Legend folder. Find them in the arcana shop, or install **Unlock Mod Content** to
have them unlocked right away.

## Troubleshooting

If holding dash doesn't charge, check `BepInEx\LogOutput.log` for the lines starting with
`Dash button` and send them over.
