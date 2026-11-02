$ErrorActionPreference = 'Stop'
$testRoot = $PSScriptRoot
$sourceRoot = [IO.Path]::GetFullPath((Join-Path $testRoot '..\..\WoLMomentumSpear'))
$csc = Join-Path $env:LOCALAPPDATA 'LocalAI\toolchain\roslyn-4.12.0\tasks\net472\csc.exe'
$net35 = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.netframework.referenceassemblies.net35\1.0.3\build\.NETFramework\v3.5\mscorlib.dll'
if (!(Test-Path -LiteralPath $csc) -or !(Test-Path -LiteralPath $net35)) { throw 'Pinned Roslyn and .NET 3.5 references are required.' }
$runRoot = Join-Path $env:LOCALAPPDATA ('LocalAI\tests\MomentumSpear\' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$exe = Join-Path $runRoot 'Behavior.exe'
$sources = @((Join-Path $testRoot 'RuntimeStubs.cs'), (Join-Path $testRoot 'Behavior.cs')) + @(Get-ChildItem -LiteralPath $sourceRoot -File -Filter '*.cs' | ForEach-Object FullName)
$systemCore = Join-Path (Split-Path $net35) 'System.Core.dll'
& $csc /nologo /noconfig /nostdlib+ /langversion:7.3 /target:exe "/reference:$net35" "/reference:$systemCore" "/out:$exe" $sources
if ($LASTEXITCODE -ne 0) { throw 'Momentum Spear simulation did not compile.' }
'<configuration><startup useLegacyV2RuntimeActivationPolicy="true"><supportedRuntime version="v4.0"/></startup></configuration>' | Set-Content -LiteralPath ($exe + '.config') -Encoding utf8
$output = & $exe
$output
if ($LASTEXITCODE -ne 0) { throw 'Momentum Spear simulation failed.' }
$output | Set-Content -LiteralPath (Join-Path $runRoot 'result.txt') -Encoding utf8
"Evidence: $runRoot"
