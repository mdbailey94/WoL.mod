# Builds the mods on Windows and copies them straight into your game.
#
#   .\build.ps1                     build every mod
#   .\build.ps1 WoLThunderhead      build just one (the folder name)
#   .\build.ps1 WoLCyclone -GameDir "D:\Games\Wizard of Legend"
#
# Needs the .NET SDK (https://dotnet.microsoft.com/download, version 8) and BepInEx already in the
# game. The first run downloads the reference copies of the game's DLLs that the mods compile
# against into lib\ (nothing from there is shipped).
param(
    [string]$Mod = "",
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Wizard of Legend"
)
$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$root = $PSScriptRoot
$lib = Join-Path $root "lib"
New-Item -ItemType Directory -Force -Path $lib | Out-Null

# The same pinned reference DLLs the online build uses (.github/workflows/build.yml).
$url = "https://raw.githubusercontent.com/TheTimeSweeper/EpicWolMods/c80d122b5f2a37f1a59e540c7911c4f92103dc2a/_lib"
foreach ($dll in "Assembly-CSharp.dll", "LegendApi.dll", "UnityEngine.dll", "UnityEngine.UI.dll", "Rewired_Core.dll") {
    $dest = Join-Path $lib $dll
    if (-not (Test-Path $dest)) {
        Write-Host "Downloading $dll"
        Invoke-WebRequest "$url/$dll" -OutFile $dest -UseBasicParsing
    }
}

# BepInEx and Harmony from your own game.
$core = Join-Path $GameDir "BepInEx\core"
if (-not (Test-Path $core)) {
    throw "No BepInEx in '$GameDir'. Install BepInEx, or pass -GameDir with your game folder."
}
foreach ($dll in "BepInEx.dll", "0Harmony.dll") {
    Copy-Item (Join-Path $core $dll) $lib -Force
}

if ($Mod) {
    $projects = @(Get-Item (Join-Path $root "$Mod\$Mod.csproj"))
} else {
    $projects = Get-ChildItem $root -Directory | ForEach-Object { Get-ChildItem $_.FullName -Filter *.csproj }
}
foreach ($project in $projects) {
    Write-Host "Building $($project.BaseName)"
    dotnet build $project.FullName -c Release -nologo -v q "-p:GameDir=$GameDir"
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $($project.BaseName)" }
}
Write-Host "Done: copied into $GameDir\BepInEx\plugins. Start the game to try it."
