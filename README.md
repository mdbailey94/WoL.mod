# WoL.mod

BepInEx 5 mods for Wizard of Legend.

| Mod | Status | Description |
|-----|--------|-------------|
| [WoLCameraZoom](WoLCameraZoom/) | Working | Zooms the gameplay camera out so you can see more of the arena. |
| [WoLRollingGale](WoLRollingGale/) | Working | New Air arcana: a line of wind bursts that rolls forward and pulls enemies. Needs LegendAPI. |
| [WoLSlingshotDash](WoLSlingshotDash/) | Working (Blazing untested) | Two hold-to-charge dash arcana: Slingshot (wind bursts that knock enemies away) and Blazing Slingshot (a flaming rush that ends in a vacuum pulling enemies in). Needs LegendAPI. |
| [WoLExtendedStats](WoLExtendedStats/) | Working | Hidden arcana and wizard stats in the character menu info box, plus a post-run report: crits, biggest crit, pit knock-offs, top speed and more. Co-op aware. |
| [WoLAscension](WoLAscension/) | Working (0.2.1 untested) | Ascension levels 1–10: stacking difficulty modifiers, picked on a pixel-art prompt when you enter the run portal. |
| [WoLUnlockModContent](WoLUnlockModContent/) | Untested | Mods-menu toggle that unlocks all mod-added arcana and relics for easy testing (reversible). Needs LegendAPI. |
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
