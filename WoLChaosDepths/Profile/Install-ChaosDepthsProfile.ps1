<#
.SYNOPSIS
  Sets Chaos Depths up as its own game profile, separate from the base game.

.DESCRIPTION
  Wizard of Legend keeps running from the same install, but with a second BepInEx profile
  folder that holds ONLY Chaos Depths (its plugin, its config, its character saves). The base
  game's own BepInEx folder, with all the arcana and relic mods, is left as it is, and the
  Chaos Depths plugin is moved OUT of it, so the two never run together.

  How it works: BepInEx's loader (Doorstop) takes a --doorstop-target argument naming the
  preloader to run. Pointing it at the profile's copy makes BepInEx use the profile folder for
  plugins and config. A launcher shortcut passes that argument; launching through Steam as
  usual runs the base game profile.

  It also writes steam_appid.txt (445980) beside the game, without which the game started
  from the launcher asks Steam to relaunch it and Steam asks about the arguments every time.

  Run this once (and again after updating WoLChaosDepths.dll):
    powershell -ExecutionPolicy Bypass -File .\Install-ChaosDepthsProfile.ps1
  Optional:  -GameDir "D:\Steam\steamapps\common\Wizard of Legend"
             -ProfileDir "D:\ChaosDepths"
             -PluginZip ".\WoLChaosDepths.zip"   (a downloaded build to install from)

.NOTES
  Saves: Chaos Depths' character saves live in the profile (BepInEx\config\ChaosDepths). The
  game's own save (unlocks, gold, gems) is shared between the two profiles; a dive's gold and
  gems carry over, which the design intends.
#>
param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Wizard of Legend",
    [string]$ProfileDir = "$env:APPDATA\ChaosDepths\profile",
    [string]$PluginZip = ""
)

$ErrorActionPreference = "Stop"
$PluginName = "WoLChaosDepths"

function Say($text) { Write-Host $text }

# ---- Find the game ----
if (-not (Test-Path $GameDir)) {
    throw "Game folder not found: $GameDir (pass -GameDir)"
}
$exe = Get-ChildItem -Path $GameDir -Filter *.exe | Where-Object { Test-Path (Join-Path $GameDir ($_.BaseName + "_Data")) } | Select-Object -First 1
if (-not $exe) {
    throw "No game executable with a matching _Data folder in $GameDir"
}
$baseBepInEx = Join-Path $GameDir "BepInEx"
if (-not (Test-Path (Join-Path $baseBepInEx "core\BepInEx.Preloader.dll"))) {
    throw "BepInEx isn't installed in $GameDir (no BepInEx\core\BepInEx.Preloader.dll). Install BepInEx 5 into the game first."
}
Say "Game:    $($exe.FullName)"
Say "Profile: $ProfileDir"

# ---- steam_appid.txt ----
# Started directly, the game asks Steam to relaunch it (SteamAPI_RestartAppIfNecessary, exit
# code 53) and Steam then asks about the launch arguments every time, blocking launches until
# answered. With the app id beside the executable it runs straight away.
$appIdFile = Join-Path $GameDir "steam_appid.txt"
if (-not (Test-Path $appIdFile)) {
    Set-Content -Path $appIdFile -Value "445980" -Encoding ASCII
    Say "Wrote steam_appid.txt (445980) so the launcher starts the game directly"
}

# ---- Make the profile folder ----
$profBepInEx = Join-Path $ProfileDir "BepInEx"
foreach ($sub in "core", "plugins", "config", "patchers") {
    New-Item -ItemType Directory -Force -Path (Join-Path $profBepInEx $sub) | Out-Null
}
# BepInEx's own core files (copied, so the profile runs the same BepInEx version as the game).
Copy-Item -Path (Join-Path $baseBepInEx "core\*") -Destination (Join-Path $profBepInEx "core") -Recurse -Force
# Its config, if the base game has one (the profile gets its own copy to edit).
$baseCfg = Join-Path $baseBepInEx "config\BepInEx.cfg"
$profCfg = Join-Path $profBepInEx "config\BepInEx.cfg"
if ((Test-Path $baseCfg) -and -not (Test-Path $profCfg)) {
    Copy-Item $baseCfg $profCfg
}

# ---- The plugin: into the profile, out of the base game ----
$profPlugin = Join-Path $profBepInEx "plugins\$PluginName"
New-Item -ItemType Directory -Force -Path $profPlugin | Out-Null
$basePlugin = Join-Path $baseBepInEx "plugins\$PluginName"
$installed = $false
if ($PluginZip -and (Test-Path $PluginZip)) {
    Say "Installing the plugin from $PluginZip"
    $tmp = Join-Path $env:TEMP ("ChaosDepthsZip_" + [guid]::NewGuid().ToString("N"))
    Expand-Archive -Path $PluginZip -DestinationPath $tmp -Force
    $dll = Get-ChildItem -Path $tmp -Recurse -Filter "$PluginName.dll" | Select-Object -First 1
    if ($dll) {
        Copy-Item $dll.FullName (Join-Path $profPlugin "$PluginName.dll") -Force
        $installed = $true
    }
    Remove-Item $tmp -Recurse -Force
}
if (-not $installed -and (Test-Path (Join-Path $basePlugin "$PluginName.dll"))) {
    Say "Moving the plugin out of the base game's BepInEx into the profile"
    Copy-Item (Join-Path $basePlugin "*") $profPlugin -Recurse -Force
    $installed = $true
}
if (Test-Path $basePlugin) {
    # The base game must not run Chaos Depths: keep a dated copy beside it, then remove it.
    $backup = Join-Path $baseBepInEx ("plugins.disabled\" + $PluginName)
    New-Item -ItemType Directory -Force -Path (Split-Path $backup) | Out-Null
    if (Test-Path $backup) { Remove-Item $backup -Recurse -Force }
    Move-Item $basePlugin $backup
    Say "Base game: $PluginName moved to BepInEx\plugins.disabled (the base game no longer loads it)"
}
if (-not $installed -and -not (Test-Path (Join-Path $profPlugin "$PluginName.dll"))) {
    throw "No $PluginName.dll found: pass -PluginZip with a downloaded build, or install the plugin in the base game first."
}

# Chaos Depths' own config and saves move with it, if they were in the base game.
$baseCdCfg = Join-Path $baseBepInEx "config\mdbailey94.wol.chaosdepths.cfg"
$profCdCfg = Join-Path $profBepInEx "config\mdbailey94.wol.chaosdepths.cfg"
if ((Test-Path $baseCdCfg) -and -not (Test-Path $profCdCfg)) { Move-Item $baseCdCfg $profCdCfg }
$baseSaves = Join-Path $baseBepInEx "config\ChaosDepths"
$profSaves = Join-Path $profBepInEx "config\ChaosDepths"
if ((Test-Path $baseSaves) -and -not (Test-Path $profSaves)) {
    Move-Item $baseSaves $profSaves
    Say "Character saves moved to the profile: $profSaves"
}

# ---- The launcher ----
$preloader = Join-Path $profBepInEx "core\BepInEx.Preloader.dll"
$launcher = Join-Path $ProfileDir "Launch Chaos Depths.bat"
@"
@echo off
rem Chaos Depths: Wizard of Legend with the Chaos Depths profile (Steam must be running).
set CHAOSDEPTHS_PROFILE=1
cd /d "$GameDir"
start "" "$($exe.FullName)" --doorstop-enable true --doorstop-target "$preloader" -screen-fullscreen 1
"@ | Set-Content -Path $launcher -Encoding ASCII
Say "Launcher: $launcher"

# A desktop shortcut to it.
try {
    $shell = New-Object -ComObject WScript.Shell
    $lnk = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath("Desktop")) "Chaos Depths.lnk"))
    $lnk.TargetPath = $launcher
    $lnk.WorkingDirectory = $GameDir
    $lnk.IconLocation = "$($exe.FullName),0"
    $lnk.Description = "Wizard of Legend: Chaos Depths"
    $lnk.Save()
    Say "Desktop shortcut: Chaos Depths"
} catch {
    Say "Couldn't make a desktop shortcut ($($_.Exception.Message)); use the launcher .bat"
}

Say ""
Say "Done. Steam launches the base game (its arcana and relic mods, no Chaos Depths);"
Say "the Chaos Depths shortcut launches the same game with only Chaos Depths."
Say "Check the profile's BepInEx\LogOutput.log for 'Chaos Depths ... loaded (profile)'."
