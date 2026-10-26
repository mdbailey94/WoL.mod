# Vacuum Fist, Blazing Kick, Feint Swap, Vine Slingshot and Charged Leap (Wizard of Legend)

Four new **dash** arcana and one standard arcana (Feint Swap), added with
[LegendAPI](https://github.com/yekoc/LegendAPI). The dashes all charge the same way; they differ
in what the launch does.

### Charging (the four dashes)

- **Tap** dash (slingshot ready): the arcana's own dash at **normal dash length**, with its
  effects at their smallest; it uses the slingshot's charge like a charged one. While the
  slingshot is recharging, tapping or holding is a plain dash.
- **Hold** dash (slingshot ready): your wizard hops backward, like pulling back a slingshot,
  and holds that pose while their element gathers round them: a fast-spinning whirlwind for
  Vacuum Fist, flames (and a trembling wizard) for Blazing Kick, ice crystals forming in a
  closing ring for Feint Swap, pebbles and leaves for Vine Slingshot, and electric arcs and
  sparks for Charged Leap. The charge is full after 1 second; after 2 seconds it
  launches by itself. Keep aiming to turn and line up the shot (Vine Slingshot is the
  exception: no hop, and no turning).
- **Release**: launch the way you're aiming. A full charge gives **+60% dash speed and +40% dash
  time** (about 2.2× the distance).

### Vacuum Fist (Air)

> **Vacuum Fist**: Hold to charge, then release to rush across the room on a howling wind that
> drags enemies along, and finish with a punch that pulls them all in!
>
> *Enhanced: Leaves a trail of gusts along your path!*

- A wide gust grabs enemies where you launch, then the wind you ride **drags everyone you pass
  along with you**.
- You wind up a punch for the end of the rush, and it lands in a **vacuum** that keeps pulling
  everyone nearby in for about half a second.
- Everything scales with charge.
- **Enhanced**: gusts are left all along your path, each blowing twice (3 damage a gust, pushing
  enemies away from the line).
- **Look** (0.25.0): a drawn ring of wind crescents spins round your feet while you charge (its
  far side behind you, its near side in front), growing with the charge and snapping tight at
  full. The launch bursts a fan of speed lines out behind you with the game's air shockwaves, and
  you rush in the wizard's own dash streaks, leaving curling gust streaks and pale afterimages.
  The punch lands in a drawn implosion (wind spiralling in to a pop), a ring on the floor closing
  in (again with each vacuum pulse), an impact star and the game's air vortex.

### Blazing Kick (Fire)

> **Blazing Kick**: Hold to charge, then release to blitz through enemies in a trail of flame,
> shoving them aside, and finish with a kick that blasts them away!
>
> *Enhanced: Sets every enemy it touches on fire!*

- A wide burst where you launch, then you rush forward wrapped in **Blazing Blitz's flame
  trail**, dropping flame bursts that **shove enemies out of your way**.
- You swing into a kick for the end of the rush, and it lands in a **fiery blast** that throws
  everyone nearby away.
- Everything scales with charge.
- **Enhanced**: every hit, trail and blast alike, **sets the enemy on fire**.
- **Look** (0.25.0): while you charge, a ring of drawn flames lights up round your feet, more and
  taller as it builds, with embers drifting up, flaring with the game's ring of fire at full. You
  take off in a small blast, leave guttering flames, embers, scorch marks and smoke along the
  rush, and the kick lands in a drawn fireball that bursts into a ring of fire with smoke rising
  inside, a scorch ring on the floor, an impact star and a spray of embers, with the game's fire
  ring, large scorch mark and smoke.

### Feint Swap (Water) - a standard arcana

> **Feint Swap**: Hold to charge, then release to hurl an ice feint through your foes; press again
> to swap places with it, and it explodes in a freezing blast!
>
> *Faster charge; leaves an exploding feint behind. Signature: hurl 6 feints, then chain-swap
> through them all!* (the game shows this one line for both the enhanced and the signature version)

Not a dash any more: it goes in a normal arcana slot, and your dash stays your dash.

- **Hold** the button: your wizard stands and gathers ice crystals round their feet (dash to
  cancel). Let go and they **hurl an ice copy of themselves** (the game's ice decoy) along your
  aim, **hitting enemies along its path** (ice bursts, 4 damage and a little knockback). A tap
  throws it a bit further than a normal dash, a 1 second hold up to 10.5, stopping short of walls.
  Held 2 seconds, it throws by itself. The cooldown (7 s by default) starts with the throw.
- It **stands** there while you move about, drawing enemies to it (it's on your side, like the
  game's own Ice Dash decoy, so your arcana pass it by) for up to **2 seconds**: **press the
  button again** (or cast again once the cooldown is up) to **swap places** with it sooner, or it
  swaps by itself when the time is up; either way it **explodes** round you (a Frost Nova, 15
  damage and a sure freeze). The only thing that stops the swap is enemies **breaking it** first,
  and then it just shatters, with no explosion. The press that threw it never counts as the re-press: the button has to come up
  first (and a press in the first quarter second after the throw is ignored), so a tap or a
  button still held when it throws itself leaves the feint waiting. (Up to 0.23.4 the feint
  shattered as it landed: it wasn't set up as an ally, so its own ice bursts broke it.)
- **Enhanced**: full range at **0.5 seconds** of holding, and a **new feint is left
  where you swapped from**, drawing enemies to it, and **explodes the same way 2 seconds later**.

**Signature** (0.24.0; put Feint Swap in the signature slot and cast it with the gauge full):

- **Fan throw**: hold and release as usual, and **six feints** fly out at once in a **fan 110
  degrees wide** centred on your aim, as far as the normal throw at that charge (each stopping
  short of walls), each bursting through the enemies on its way (3 damage a burst, and the six
  can't stack on one enemy: one burst every 0.15 s at most). A frosty flash, the room dims a
  little and the camera pulls back to show the fan.
- **Wait**: they stand for 2 seconds, or until you press again / cast again (same rules as the
  normal feint: the press that threw them never counts).
- **Chained swaps**: you swap onto them **one after another, 0.2 s apart**, sweeping across the
  fan from its **left end to its right** (left as seen facing along your aim). Each one you land
  on goes off in an **icicle spin**: a ring of 8 icicles (the game's ice shard sprite) spins out
  from you and bursts into the game's icicle shards with a Frost Nova (17 damage, a sure freeze,
  radius 1.75), with a 2-frame hit-stop and a shake that grows with each swap. The **last one**
  is bigger: 12 icicles, radius 2.4, 22 damage and a 2 s freeze, a longer hit-stop, a big shake,
  a flash and the room lighting up before settling back. Feints enemies broke before their turn
  are **skipped**.
- Enhanced as well (the usual case, since the game empowers whatever sits in the signature
  slot): the charge is full in half the time, and a feint is left **where you stood when the
  chain began**, exploding 2 s later (15).
- Its hits don't refill the gauge. The camera, time and light always go back to normal: when the
  last spin settles, when the cast is cut short (all feints broken, the wizard gone or dead), on
  a level load, and by a watchdog if anything ever runs long.
- The signature is unlocked together with the arcana (`SignatureUnlocksWithArcana`, under
  `[General]`, default on); off, its Signature drop has to be found in a run like the game's own.

(The old dash version is still in the mod, hidden, so a save that has it equipped still loads;
the shop offers the new one.)

### Vine Slingshot (Earth)

> **Vine Slingshot**: Hold to lash out twin vines that grab the first foe they touch, then
> release to pull yourself in and kick it away!
>
> *Enhanced: Vines spread to nearby foes and keep them snared for 3 seconds after the kick!*

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
- **Enhanced**: when the vines take hold, more vines **spread to up to 5 enemies** within 4 tiles,
  coiling round them and rooting them (2 damage every 0.5 s) while you hold and for **3 seconds
  after the kick**, anchored where the kick landed.
- **Look** (0.25.0): the punch into the ground sends a drawn shockwave across the floor and
  throws up tumbling rocks and dust; the game's thorns burst where the vines grab an enemy. While
  you hold, stones keep hopping up out of the floor. The pull kicks up dust puffs and rocks
  behind you, and the kick bursts into a cone of drawn rock shards along it, a floor shockwave,
  an impact star and rocks thrown on ahead, with the game's floor crater; the target skids away
  in puffs of dust.

### Charged Leap (Lightning)

> **Charged Leap**: Hold to pull enemy projectiles into orbit around you, then release to fling
> them at the foe and leap in after them!
>
> *Enhanced: A bigger lightning burst where you land!*

- While you hold, **Mag Sphere** forms on you and works at full effect, exactly like the arcana,
  just **smaller** (`StormSphereSize`). Nothing it holds can **hit you or your allies** while
  you charge, and anything about to fly out of the smaller sphere is turned back into its orbit.
- Let go and the sphere ends and **everything it caught is fired in a fan along your aim**,
  hitting harder than before (×1.5), while you **dash after them** and land with a small
  **lightning burst**.
- **Enhanced**: the landing burst is half as big again.
- **Look** (0.25.0): a drawn ring of static crackles round your feet while you charge, with sparks
  snapping round you and the game's lightning aura at full. The leap fires a drawn bolt along your
  aim with the game's lightning start, and you go in the wizard's dash streaks, leaving yellow and
  violet afterimages and sparks. The landing is a drawn thunder burst with a shock ring on the
  floor and a bolt struck back along the whole leap, with the game's lightning hits and a scorch
  mark (all bigger with the enhanced burst).

### Flair

Every arcana has extra effects that are only for show (they never hit anything or change the
timing): Vacuum Fist's whirl of wind, the punch it winds up and the dust drawn into its vacuum,
Blazing Kick's fiery blasts and the kick it swings into at the end of its rush, Feint Swap's splashes,
shimmering trail and glints at both ends, and Charged Leap's lightning flash and crackling sparks.
Most come with a small camera shake.

The four dashes' **Look** effects (above) mix the game's own effects with pixel art the mod draws
in code (`PixelArt.cs`): 4 to 8 frame animations in a 5 to 7 colour palette per element, at the
game's 16 pixels per unit, sorted with the wizard, on the effects layer or flat on the floor. They
are pooled (at most 160 on screen), stop when the dash ends and are cleared on a level load. The
log says `<arcana>: visual '<name>' ready (...)` the first time each one plays, and
`<arcana>: look '<moment>' played (...)` the first time each moment of a dash shows.

### Balance

Slingshot arcana trade a long cooldown and a charge-up for more range and damage than a normal
dash, without outclassing standard arcana. The **slingshot** has one charge that recharges in
**7 s** (`CooldownSeconds`); until then, and for taps, the dash button is a **normal dash** with a
short cooldown. The arcana's HUD icon shows only the **slingshot's** cooldown (counting down
while it recharges, and flashing when it's ready), not the normal dash's; a puff of dust and a
swish also tell you it's ready.

| | Damage per enemy | Notes |
|---|---|---|
| Vacuum Fist | 4 per drag, 2 per vacuum pulse (about 20 at most) | Drags along, then pulls in; enhanced trail gusts 3 each |
| Blazing Kick | 4 per trail burst, 10 from the blast (about 18 at most) | Shoves aside, then throws away; enhanced, every hit burns |
| Feint Swap | 4 per ice burst on the path (one or two an enemy), 15 from the explosion (about 23 at most) | A sure freeze for 1.5 s; the feint draws enemies; enhanced, a second explosion where you were. 0.23.7 trimmed the path bursts from 6: a sure freeze plus a teleport keeps the damage low |
| Feint Swap signature | 17 per icicle spin (22 the last), 3 per burst on the path | One spin on an enemy is about 20-23 with a burst or two. Six feints landing on one spot (stopped by a wall in front of you) can't pass 5 x 17 + 22 + 9 = 116, 131 with the enhanced feint left behind, in line with the game's own signatures (~120 on one enemy) |
| Vine Slingshot | 3 per grip tick (every 0.4 s, up to 2 s) and 14 from the kick | Grip stuns; the kick knocks back hard; enhanced snares 2 every 0.5 s |
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
- `Drag`: how hard Vacuum Fist's wind drags enemies along your dash. Default `35`; make it
  negative if they get pushed the wrong way.
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

**Icons:** Vacuum Fist, Blazing Kick, Charged Leap and Feint Swap use the player-made pixel icons
from the "Dash Arcana Icons" design, as drawn. Under `[Icons]`, `MatchGamePalette` (default on)
recolours Vine Slingshot's icon in the colours of the game's own vine arcana, once the game has
loaded them (it doesn't hook any game code); the game icons it used are saved to
`BepInEx/config/SlingshotDash_IconRefs`. Turn it off for the mod's own colours.

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
  constants at the top or numbers in the calls. Feint Swap is `FeintSwapState.cs`, its signature
  `FeintSwapSignature.cs` (fan, timings, spin sizes), `IcicleSpin.cs` (the icicle rings) and
  `FeintCinematic.cs` (flash, light, camera pull-back, hit-stops).
- **The dashes' drawn effects**: the drawings and palettes in `PixelArt.cs`, the sprites that play
  them (pool, sorting, afterimages) in `PixelFx.cs`, and when and where each plays in the arcana's
  state file (the "look" constants and the `...Look` methods).

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
`Dash button` and send them over. For Feint Swap's signature, the lines starting with
`Feint Swap: signature` (and `Feint Swap: cast (`) tell each beat of the cast.
