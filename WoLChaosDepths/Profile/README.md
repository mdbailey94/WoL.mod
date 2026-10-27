# Chaos Depths as its own game profile

Chaos Depths runs as a separate profile of Wizard of Legend, so it never mixes with the base game
and the arcana and relic mods that live there.

- **Steam** launches the base game as always: the base `BepInEx` folder with all the ability and
  relic mods, and no Chaos Depths.
- **The "Chaos Depths" shortcut** launches the same install with a second BepInEx profile folder
  that holds only Chaos Depths: its plugin, its config, its character saves.

Both use the same game executable and the same game save (unlocks, gold, gems), which is what
the design intends: a dive's gold and gems carry over.

## Setting it up

1. Download the latest `WoLChaosDepths.zip` (or have the plugin installed in the base game).
2. Open PowerShell in this folder and run:

   ```
   powershell -ExecutionPolicy Bypass -File .\Install-ChaosDepthsProfile.ps1 -PluginZip ..\WoLChaosDepths.zip
   ```

   Leave out `-PluginZip` to move the plugin that is already in the base game. Pass `-GameDir`
   if the game isn't in the default Steam folder, and `-ProfileDir` to put the profile somewhere
   other than `%APPDATA%\ChaosDepths\profile`.

3. The script writes `steam_appid.txt` beside the game (so the launcher can start it directly
   without Steam asking about the arguments), copies BepInEx's core into the profile, puts the plugin (and any existing Chaos
   Depths config and saves) there, **moves the plugin out of the base game** into
   `BepInEx\plugins.disabled`, and writes `Launch Chaos Depths.bat` plus a desktop shortcut.
4. Launch from the shortcut (Steam must be running). The profile's `BepInEx\LogOutput.log` says
   `Chaos Depths ... loaded (profile)`, and the Hub shows a small "CHAOS DEPTHS" tag.

Updating: run the script again with the new zip. It only touches the Chaos Depths files.

## How it works

BepInEx's loader (UnityDoorstop) accepts `--doorstop-target <preloader>` on the command line.
The launcher points it at the profile's copy of `BepInEx.Preloader.dll`, and BepInEx then uses
that profile's `plugins` and `config` folders (the same mechanism mod managers use for profiles).
Launched without the argument, the game uses its own `doorstop_config.ini` and the base folder.

## Known limits

- The game save is shared. In the profile the mod arcana don't exist, so a loadout that uses
  them shows vanilla arcana there; whether the game rewrites the saved loadout when it can't
  find them is being checked. If it does, keep a spare loadout or re-pick after a profile session.
- The launcher passes `-screen-fullscreen 1`; take it out of the .bat to play windowed.
