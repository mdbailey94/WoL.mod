# WoL.mod

BepInEx 5 mods for Wizard of Legend.

| Mod | Status | Description |
|-----|--------|-------------|
| [WoLCameraZoom](WoLCameraZoom/) | Working | Zooms the gameplay camera out so you can see more of the arena. |
| [WoLRollingGale](WoLRollingGale/) | Working | New Air arcana: a line of wind bursts that rolls forward and pulls enemies. Needs LegendAPI. |
| [WoLSlingshotDash](WoLSlingshotDash/) | Working (Storm untested) | Five hold-to-charge dash arcana: Vacuum Fist (a rushing wind that drags enemies, ending in a vacuum punch), Blazing Kick (a flaming rush that shoves enemies aside, ending in a blast), Feint Swap (throw an ice feint, swap places, freeze both ends) Vine Slingshot (grab with twin vines, pull in and kick) and Charged Leap (catch enemy projectiles, then hurl them and yourself). Needs LegendAPI. |
| [WoLExtendedStats](WoLExtendedStats/) | Working | Hidden arcana and wizard stats in the character menu info box, plus a post-run report: crits, biggest crit, pit knock-offs, top speed and more. Co-op aware. |
| [WoLAscension](WoLAscension/) | Working (0.4.0 untested) | Ascension levels 1–10: stacking difficulty modifiers, set at an altar beside the trials portal, with +4% chaos gems per level. |
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

To build locally instead, see each mod's README.
