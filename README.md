# WoL.mod

BepInEx 5 mods for Wizard of Legend.

| Mod | Status | Description |
|-----|--------|-------------|
| [WoLCameraZoom](WoLCameraZoom/) | Working | Zooms the gameplay camera out so you can see more of the arena. |
| [WoLRollingGale](WoLRollingGale/) | Working | New Air arcana: a line of wind bursts that rolls forward and pulls enemies. Needs LegendAPI. |
| [WoLExtendedStats](WoLExtendedStats/) | Working | Hidden arcana and wizard stats in the character menu info box, plus a post-run report: crits, biggest crit, pit knock-offs, top speed and more. Co-op aware. |
| [WoLModMenu](WoLModMenu/) | Untested | A MODS panel on the title screen to switch the mods above on and off instantly (Select / M). |

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
