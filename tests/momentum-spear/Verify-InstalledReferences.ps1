param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Wizard of Legend')
$ErrorActionPreference = 'Stop'
$sourceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\WoLMomentumSpear'))
$csc = Join-Path $env:LOCALAPPDATA 'LocalAI\toolchain\roslyn-4.12.0\tasks\net472\csc.exe'
$framework = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.netframework.referenceassemblies.net35\1.0.3\build\.NETFramework\v3.5'
$references = @(
    (Join-Path $framework 'mscorlib.dll'), (Join-Path $framework 'System.dll'), (Join-Path $framework 'System.Core.dll'),
    (Join-Path $GameDir 'WizardOfLegend_Data\Managed\Assembly-CSharp.dll'),
    (Join-Path $GameDir 'WizardOfLegend_Data\Managed\UnityEngine.dll'),
    (Join-Path $GameDir 'BepInEx\core\BepInEx.dll'), (Join-Path $GameDir 'BepInEx\core\0Harmony.dll'), (Join-Path $GameDir 'BepInEx\plugins\LegendApi.dll')
)
$sources = @(Get-ChildItem -LiteralPath $sourceRoot -File -Filter '*.cs' | Sort-Object Name | ForEach-Object FullName)
$before = @($sources + $references | ForEach-Object { [pscustomobject]@{file=$_;sha256=(Get-FileHash -LiteralPath $_).Hash} })
$runRoot = Join-Path $env:LOCALAPPDATA ('LocalAI\tests\MomentumSpear\installed-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$dll = Join-Path $runRoot 'WoLMomentumSpear.dll'
$arguments = @('/nologo', '/noconfig', '/nostdlib+', '/langversion:7.3', '/target:library', "/out:$dll") + @($references | ForEach-Object { "/reference:$_" }) + $sources
$diagnostics = & $csc $arguments
$exitCode = $LASTEXITCODE
$unchanged = @($before | Where-Object { (Get-FileHash -LiteralPath $_.file).Hash -ne $_.sha256 }).Count -eq 0
$result = [pscustomobject]@{passed=($exitCode -eq 0 -and $unchanged);exit_code=$exitCode;current_source_and_references_unchanged=$unchanged;diagnostics=($diagnostics -join "`n");output=$dll;inputs=$before;installed=$false;gameplay_tested=$false}
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'installed-reference-result.json') -Encoding utf8
$result | ConvertTo-Json -Depth 5
if (!$result.passed) { throw 'Installed-reference compilation failed or inputs changed.' }
