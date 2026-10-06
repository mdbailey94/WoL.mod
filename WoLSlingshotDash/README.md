# Raging Wind, Vacuum Kick, Feint Swap, Vine Slingshot and Charged Leap (Wizard of Legend)

Five new **dash** arcana, added with [LegendAPI](https://github.com/yekoc/LegendAPI). Both charge
the same way; they differ in what the launch does.

### Charging (all five)

- **Tap** dash: a normal dash. While the slingshot is recharging, holding is a normal dash too.
- **Hold** dash (slingshot ready): your wizard hops backward, like pulling back a slingshot, then holds that pose
  while dust builds at their feet. The charge is full after 1 second; after 2 seconds it
  launches by itself. Keep aiming to turn and line up the shot.
- **Release**: launch the way you're aiming. A full charge gives **+60% dash speed and +40% dash
  time** (about 2.2× the distance).

### Raging Wind (Air)

> **Raging Wind**: Hold to pull back and charge, then release to ride a raging wind across the
> room, blasting enemies away where you leave and land!

Wind bursts go off **where you leave and where you land**, knocking nearby enemies away: hard at
the launch, gentler where you land. Both get bigger with more charge.

### Vacuum Kick (Fire)

> **Vacuum Kick**: Hold to pull back and charge, then release to blitz across the room in
> flames, dragging enemies along, and land a kick that pulls them in!

- A wide burst catches enemies where you launch, then you rush forward wrapped in **Blazing
  Blitz's flame trail**, dropping flame bursts that drag enemies they hit along your path.
- Where you land, a **flame vacuum** keeps sucking everyone nearby in for about half a second.
- Everything scales with charge, and it's Fire, so it can burn.

### Feint Swap (Water)

> **Feint Swap**: Hold to pull back and charge, then release to throw an ice feint and swap
> places with it, freezing enemies at both ends!

- Hold **0.2 to 1 second**, then release: instead of dashing, you **throw an ice copy of
  yourself** (the game's ice decoy) along your aim, 3 to 9 tiles depending on how long you held,
  stopping short of walls. Shorter than 0.2 seconds is a normal dash.
- It **hovers** while you keep moving: from no hover at 0.2 seconds up to **2 seconds** at a full
  charge. Then, or as soon as you **press dash again**, you **swap places**: you appear where it
  is, and it appears where you are.
- **Both spots freeze**: a small Frost Nova bursts at each end, and the copy lingers for a couple
  of seconds so enemies keep going after it.

### Vine Slingshot (Earth)

> **Vine Slingshot**: Hold to lash out twin vines that grab the first foe they touch, then let go
> to pull yourself in and kick it away!

- Hold dash past a tap and the wizard **punches the ground** as **twin vines** (the game's own
  vine, as thrown by Soaring Ivy) **shoot out** along your aim, up to 10.1 tiles. You stay planted
  and **can't turn** while holding; that's the trade-off for the longer reach.
- The **first enemy they touch is grabbed**: vines coil tight around it (on bosses too, even
  though they can't be held still) and it takes small hits that keep it stunned for as long as
  you hold, up to 2 seconds.
- If they touch no enemy, they **latch onto the wall** (or the ground at their reach).
- Let go, or run out of time, and you **pull yourself in** trailing dust and pebbles, with a kick
  that **knocks the target back** hard: a hit-stop, a camera shake, a floor crack and a spray of
  rock, and the target skids away in a cloud of dust. The kick also hits **enemies just behind
  the target**, knocking them the same way.

### Charged Leap (Lightning)

> **Charged Leap**: Hold to pull enemy projectiles into orbit around you, then let go to fling
> them at the foe and leap in after them!

- While you hold, **Mag Sphere** forms on you and works at full effect, exactly like the arcana,
  just **smaller** (`StormSphereSize`). Nothing it holds can **hit you or your allies** while
  you charge, and anything about to fly out of the smaller sphere is turned back into its orbit.
- Let go and the sphere ends and **everything it caught is fired in a fan along your aim**,
  hitting harder than before (×1.5), while you **dash after them** and land with a small
  **lightning burst**.

### Flair

Every arcana has extra effects that are only for show (they never hit anything or change the
timing): Raging Wind's take-off puff and streaming gust, Vacuum Kick's fiery blasts, the kick it
swings into at the end of its rush and the dust drawn into its landing, Feint Swap's splashes,
shimmering trail and glints at both ends, and Charged Leap's lightning flash and crackling sparks.
Most come with a small camera shake.

### Balance

Slingshot arcana trade a long cooldown and a charge-up for more range and damage than a normal
dash, without outclassing standard arcana. The **slingshot** has one charge that recharges in
**7 s** (`CooldownSeconds`); until then, and for taps, the dash button is a **normal dash** with a
short cooldown. The arcana's HUD icon shows only the **slingshot's** cooldown (counting down
while it recharges, and flashing when it's ready), not the normal dash's; a puff of dust and a
swish also tell you it's ready.

| | Damage per enemy | Notes |
|---|---|---|
| Raging Wind | 10 at launch, 6 where you land (up to 16) | Strong knockback at launch, gentle at landing |
| Vacuum Kick | 4 per trail burst, 2 per vacuum pulse (about 20 at most) | Plus burn; pulls instead of pushing |
| Feint Swap | 12 per Frost Nova (one at each end) | Freezes for 1.5 s; the feint draws enemies |
| Vine Slingshot | 3 per grip tick (every 0.4 s, up to 2 s) and 14 from the kick | Grip stuns; the kick knocks back hard |
| Charged Leap | Caught projectiles ×1.5, plus 10 from the landing burst | Only as strong as what you catch |

You're not invulnerable while charging, so time it. Careful hopping backward near a ledge.

## Settings

The hop and the held pose reuse the game's own wizard animations. In
`BepInEx\config\mdbailey94.wol.slingshotdash.cfg`, under `[Charge]`:

- `Animation`: `Jump` (default), `Charge`, `Slide`, `Hurt`, `Kick`, `Parry`, `Slam`, `Fall` or `None`.
- `PoseFrame`: which moment of that animation is held, from `0` (start) to `1` (end). Default `0.4`.
- `HopDistance`: how far the hop goes. Default `1.5`; `0` turns the hop off.
- `MaxHold`: every slingshot launches by itself after you've held this long. Default `2`; `0`
  lets you hold as long as you like.

Under `[Balance]`:

- `CooldownSeconds`: how long until you can slingshot again. Default `7`.
- `DashCooldownSeconds`: cooldown of the normal dash in between. Default `0.6`.
- `BlazingDrag`: how hard Vacuum Kick's trail drags enemies along your dash. Default
  `35`; make it negative if they get pushed the wrong way.
- `FrostFreezeRadius`: size of Feint Swap's freezes at the shortest hold (a full charge adds
  half again). Default `1`.
- `StormSphereSize`: size of Charged Leap's Mag Sphere compared with the arcana's (`1` = the
  same). Default `0.6`.

Under `[Vines]`: `DarkColor`, `MidColor` and `LightColor` set Vine Slingshot's greens as hex
colours like `#2E6B3A`. Empty (the default) takes them from the game's own vine art; the log
says which colours it used. `HoldFrame` picks the moment of the game's vine animation that's held
for the whole grab (`0` = its start, `1` = its end). Default `0.95`, the fully stretched vines,
which are then stretched further to reach the enemy, wall or ground they grabbed.
`HoldAnimation` (default `PBAoE`, punching the ground; `Slam` is the other ground hit) and
`HoldPoseFrame` (default `0.5`) set the wizard's pose while holding Vine Slingshot.

Changes apply the next time the game starts.

## Changing it in code

Anything not in the config file lives in the code:

- **Damage, knockback, freeze, element, shop price**: the `Skills.Register(...)` blocks in
  `SlingshotDashPlugin.cs`, one per arcana. Stat arrays have one entry per skill level; where
  there are two (e.g. `damage = new[] { 10, 6 }`), level 1 is the launch and level 2 the
  landing, trail or pulses, as the comments say.
- **Charging** (time to full charge, tap window, speed/duration bonus, hop timing): the
  constants at the top of `ChargedDashState.cs`.
- **Each arcana's attacks**: `SlingshotDashState.cs` (Air), `BlazingSlingshotState.cs` (Fire),
  `FrostSlingshotState.cs` (Water), `VineSlingshotState.cs` (Earth; the vines are drawn in
  `VineLines.cs`) and `StormSlingshotState.cs` (Lightning): burst sizes, intervals, throw distance and so on, as
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
