#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$UnityEditor = 'E:/Unity/2018.3.3f1/Editor/Unity.exe',
    [switch]$CompileOnly
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$work = Join-Path $repo ('outputs/shadow-override-tests/' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($work)
$source = Join-Path $repo 'Assets/Scripts/Manager/BenchmarkShadowOverride.cs'
$compiler = "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe"
$editorDirectory = Split-Path -Parent $UnityEditor

# Editor assemblies expose Light.lightmapBakeType, but Windows player assemblies do not.
# Compile against both real player API variants to catch this before a Unity build.
foreach ($variant in @('win64_nondevelopment_mono','win64_development_mono')) {
    $core = Join-Path $editorDirectory "Data/PlaybackEngines/windowsstandalonesupport/Variations/$variant/Data/Managed/UnityEngine.CoreModule.dll"
    if (!(Test-Path -LiteralPath $core)) { throw "Missing player API assembly: $core" }
    & $compiler /nologo /target:library "/out:$work/ShadowOverride-$variant.dll" "/reference:$core" $source
    if ($LASTEXITCODE -ne 0) { throw "Shadow override does not compile against $variant." }
}
Write-Host 'PASS: Release/Development Windows player API compilation.'
if ($CompileOnly) { return }
$testExecutable = Join-Path $work 'ShadowOverrideTests.exe'
& $compiler /nologo "/out:$testExecutable" $source (Join-Path $PSScriptRoot 'ShadowOverrideTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Cannot compile shadow lifecycle tests.' }
& $testExecutable
if ($LASTEXITCODE -ne 0) { throw 'Shadow lifecycle tests failed.' }
Write-Host 'PASS: Release/Development Windows player API compilation and shadow lifecycle. No game or measurements launched.'
