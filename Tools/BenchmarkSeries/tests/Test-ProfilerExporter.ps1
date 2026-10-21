#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$UnityEditor = 'E:/Unity/2018.3.3f1/Editor/Unity.exe',
    [string]$NativeSegment
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$work = Join-Path $repository ('outputs/profiler-exporter-tests/' + [Guid]::NewGuid().ToString('N'))
$editorScripts = Join-Path $work 'Assets/Editor'
[void][IO.Directory]::CreateDirectory($editorScripts)
Copy-Item -LiteralPath (Join-Path $repository 'Assets/Editor/BenchmarkProfilerExporter.cs') -Destination $editorScripts
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ProfilerExporterTests.cs') -Destination $editorScripts
function Invoke-TestUnity([string]$Method, [string]$Log, [string[]]$Extra = @()) {
    $arguments = @('-batchmode', '-nographics', '-quit', '-projectPath', $work, '-executeMethod', $Method, '-logFile', $Log) + $Extra
    $quoted = ($arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $process = Start-Process -FilePath $UnityEditor -ArgumentList $quoted -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Unity test failed. Read $Log" }
}
function Check($Condition, [string]$Message) { if (!$Condition) { throw $Message } }
$testLog = Join-Path $work 'tests.log'
if (!$NativeSegment) {
    Invoke-TestUnity 'ProfilerExporterTests.Run' $testLog
} else {
    $NativeSegment = (Resolve-Path -LiteralPath $NativeSegment).Path
    Invoke-TestUnity 'ProfilerExporterTests.VerifyNativeAllocations' $testLog @('-raw', $NativeSegment)
    $logText = Get-Content -LiteralPath $testLog -Raw
    $match = [regex]::Match($logText, 'NATIVE_CROSSCHECK allocBytes=(\d+) allocCalls=(\d+) spawnBytes=(\d+) spawnCalls=(\d+) despawnBytes=(\d+) despawnCalls=(\d+) replaceBytes=(\d+) replaceCalls=(\d+)')
    Check $match.Success 'Missing native numeric-column cross-check.'
    $fixtures = Join-Path $work 'fixtures'
    $repeated = Join-Path $fixtures 'repeated'
    $missing = Join-Path $fixtures 'missing'
    [void][IO.Directory]::CreateDirectory($repeated)
    [void][IO.Directory]::CreateDirectory($missing)
    Copy-Item -LiteralPath $NativeSegment -Destination (Join-Path $repeated 'segment.raw')
    $manifest = @{result='Completed'; unityVersion='2018.3.3f1'; scenario='OverlapTest'; allocation='InstantiateDestroy'; removed=[int]$match.Groups[8].Value; segments=@(@{file='segment.raw'}, @{file='segment.raw'})}
    [IO.File]::WriteAllText((Join-Path $repeated 'profiler.json'), ($manifest | ConvertTo-Json -Depth 5))
    $manifest.scenario = 'MissingTest'; $manifest.segments = @(@{file='missing.raw'})
    [IO.File]::WriteAllText((Join-Path $missing 'profiler.json'), ($manifest | ConvertTo-Json -Depth 5))
    $destination = Join-Path $work 'export'
    Invoke-TestUnity 'BenchmarkProfilerExporter.ExportBatch' (Join-Path $work 'export.log') @('-benchmarkProfilerInput', $fixtures, '-benchmarkProfilerOutput', $destination)
    $rows = @(Import-Csv -LiteralPath (Join-Path $destination 'summary.csv'))
    $bad = $rows | Where-Object scenario -eq 'MissingTest'
    Check ($bad.status -eq 'Invalid' -and $bad.spawnTotalMs -eq '') 'Missing data appeared as zero/valid.'
    $good = $rows | Where-Object scenario -eq 'OverlapTest'
    Check ([int]$good.duplicateSamplesRemoved -gt 0) 'Repeated segment was not deduplicated.'
    Check ($good.spawnAllocBytes -eq $match.Groups[3].Value -and $good.spawnCalls -eq $match.Groups[4].Value) 'Spawn attribution differs from native numeric column.'
    Check ($good.despawnAllocBytes -eq $match.Groups[5].Value -and $good.despawnCalls -eq $match.Groups[6].Value) 'Despawn attribution differs from native numeric column.'
    Check ($good.replacementAllocBytes -eq $match.Groups[7].Value -and $good.replacementCalls -eq $match.Groups[8].Value) 'Replacement attribution differs from native numeric column.'
    Check (!(Test-Path -LiteralPath (Join-Path $destination 'INCOMPLETE.txt'))) 'Export did not finish.'
}
Check ((Get-Content -LiteralPath $testLog -Raw).Contains('Profiler exporter regression tests PASSED.')) 'Regression tests did not complete.'
Write-Host "Profiler exporter tests PASSED. Artifacts: $work"
