# WoL.mod

BepInEx 5 mods for Wizard of Legend.

| Mod | Status | Description |
|-----|--------|-------------|
| [WoLCameraZoom](WoLCameraZoom/) | Working | Zooms the gameplay camera out so you can see more of the arena. |
| [WoLRollingGale](WoLRollingGale/) | Working | New Air arcana: a line of wind bursts that rolls forward and pulls enemies. Needs LegendAPI. |
| [WoLCyclone](WoLCyclone/) | Untested | New Air standard arcana: hold to grow a steerable twister into a hurricane over 4 s (bigger, faster, harder hits), bursting a second later to blow everyone away. Needs LegendAPI. |
| [WoLThunderhead](WoLThunderhead/) | Untested | New Lightning standard arcana: leap high into the air out of reach (like Heroic Leap) as lightning rains down around you, then crash down in a lightning slam. Needs LegendAPI. |
| [WoLTrailblazer](WoLTrailblazer/) | Untested | New Fire standard arcana: set yourself ablaze for a few seconds; a ring of fire burns enemies you touch and moving leaves a burning trail, growing with your run speed. Needs LegendAPI. |
| [WoLSlingshotDash](WoLSlingshotDash/) | Working (Storm untested) | Five hold-to-charge dash arcana: Vacuum Fist (a rushing wind that drags enemies, ending in a vacuum punch), Blazing Kick (a flaming rush that shoves enemies aside, ending in a blast), Feint Swap (throw an ice feint, swap places, freeze both ends) Vine Slingshot (grab with twin vines, pull in and kick) and Charged Leap (catch enemy projectiles, then hurl them and yourself). Needs LegendAPI. |
| [WoLExtendedStats](WoLExtendedStats/) | Working | Hidden arcana and wizard stats in the character menu info box, plus a post-run report: crits, biggest crit, pit knock-offs, top speed and more. Co-op aware. |
| [WoLAscension](WoLAscension/) | Working | Ascension levels 1–10: stacking difficulty modifiers, set at an altar beside the trials portal (its flame and number show the level), with +4% chaos gems per level. |
| [WoLCloakUpgrades](WoLCloakUpgrades/) | Untested | Permanent cloak upgrades bought with chaos gems in the wardrobe screen: four tiers, up to doubled bonuses (strong stats capped at +50%). |
| [WoLUnlockModContent](WoLUnlockModContent/) | Fixed (0.2.0 untested) | Mods-menu toggle that unlocks all mod-added arcana and relics for easy testing (reversible). Needs LegendAPI. |
| [WoLCustomPaintings](WoLCustomPaintings/) | Untested | Puts your own pictures on some of the breakable paintings in the Chaos Trials, framed and pixelated to fit. |
| [WoLModMenu](WoLModMenu/) | Working | A MODS panel on the title screen to switch the mods above on and off instantly (Select / M). |

## Install (no build needed)

1. Install [BepInExPack of Legend](https://thunderstore.io/c/wizard-of-legend/p/Modding_Council/BepInExPack_of_Legend/)
   (or plain BepInEx 5 x64) into your Wizard of Legend folder, then run the game once and close it.
   Skill mods also need [LegendAPI](https://thunderstore.io/c/wizard-of-legend/p/RandomlyAwesome/LegendAPI/) **and**
   [HookGenPatcher](https://thunderstore.io/c/wizard-of-legend/p/Modding_Council/HookGenPatcher/), which LegendAPI
   depends on (it goes in `BepInEx\patchers`). A mod manager like r2modman installs both for you.
2. Download the mod's `.zip` from the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest).
3. Unzip it into your Wizard of Legend folder (the one with `WizardOfLegend.exe`).
   The DLL ends up in `BepInEx\plugins\<ModName>\`.

Every push to `main` rebuilds the mods and replaces the downloads on the `latest` release.

## Changing the mods yourself

**Without code:** most mods have settings in `BepInEx\config\mdbailey94.wol.<mod>.cfg` (created the
first time the game runs with the mod). Edit the file with Notepad while the game is closed; each
setting has a comment saying what it does.

**In the code:** every number that shapes how a mod plays is a named constant near the top of its
`...State.cs` (or `...Plugin.cs` for damage and cooldowns), with a comment saying what it does. For
example, in `WoLCyclone\CycloneState.cs`:

```csharp
private const float GrowTime = 4f;         // from small twister to full hurricane
private const float Distance = 4f;         // starts about three tiles in front of the wizard
```

and its damage per hit is in `WoLCyclone\CyclonePlugin.cs` (`damage = new[] { ... }`: one number
per skill level, explained just above it). Thunderhead and Trailblazer have all of theirs in the
config file instead.

To build your change into the game (Windows):

1. Install the [.NET SDK](https://dotnet.microsoft.com/download) (version 8) once.
2. Download this repository (green **Code** button > **Download ZIP** on the branch you want, or
   `git clone`), and unzip it anywhere.
3. Change the numbers you want, save.
4. Open PowerShell in the repository folder and run, for example:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\build.ps1 WoLThunderhead
   ```

   (leave the mod name off to build them all; add `-GameDir "D:\path\to\Wizard of Legend"` if the
   game isn't in Steam's default folder). It builds the mod and copies the DLL straight into
   `BepInEx\plugins`, so just start the game.

If the build fails, it prints the file and line with the problem (usually a missing `;` or `f`
after a decimal number, like `1.5f`).
