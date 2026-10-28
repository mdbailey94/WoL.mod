# Chaos Depths as its own game profile

Chaos Depths runs as a separate profile of Wizard of Legend, so it never mixes with the base game
and the arcana and relic mods that live there.

- **Steam** launches the base game as always: the base `BepInEx` folder with all the ability and
  relic mods, and no Chaos Depths.
- **The "Chaos Depths" shortcut** launches the same install with a second BepInEx profile folder
  that holds only Chaos Depths: its plugin, its config, its character saves.

Both use the same game executable, but each has its own game save. The profile keeps its unlocks,
loadout, gold, gems and settings in a `ChaosDepths` folder beside the game's save folder, so a
profile session never rewrites the base game's loadout (the base game's mod arcana don't exist in
the profile, and the game replaces them when it can't find them). The first time the profile runs
it copies the base save in as its starting point, so nothing has to be unlocked twice.

## Setting it up

1. Download the latest `WoLChaosDepths.zip` (or have the plugin installed in the base game).
2. Open PowerShell in this folder and run:

   ```
   powershell -ExecutionPolicy Bypass -File .\Install-ChaosDepthsProfile.ps1 -PluginZip ..\WoLChaosDepths.zip
   ```

   Leave out `-PluginZip` to move the plugin that is already in the base game. Pass `-GameDir`
   if the game isn't in the default Steam folder, and `-ProfileDir` to put the profile somewhere
   other than `%USERPROFILE%\ChaosDepths\profile` (keep it outside `AppData`, see below).

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

## If the shortcut launches the normal game

The launcher only works if the profile folder exists where the shortcut's command line says. A
script run from inside a **packaged app** (the Claude desktop app is one) has its `AppData` writes
redirected into that app's private storage, so a profile made under `%APPDATA%` is invisible to a
double-click on the desktop, Doorstop finds no profile, and the base game runs. Since 0.11.8 the
default profile folder is `%USERPROFILE%\ChaosDepths\profile`, outside `AppData`; re-run the
installer and it copies an older `%APPDATA%` profile across. To check which profile ran, see which
`BepInEx\LogOutput.log` (profile or game folder) was just written.

## Known limits

- The two saves don't talk to each other after the first copy: gold, gems and unlocks earned in
  the profile stay in the profile. Delete the `ChaosDepths` save folder (under
  `%USERPROFILE%\AppData\LocalLow\Contingent99\Wizard of Legend`) to copy the base save in again.
  `[Profile] SeparateSave = false` in the mod's config goes back to a shared save.
- Steam Cloud syncs the game's save folder; the profile's folder sits inside it and most likely
  syncs too, which costs a few hundred kilobytes and nothing else.
- The launcher passes `-screen-fullscreen 1`; take it out of the .bat to play windowed.
