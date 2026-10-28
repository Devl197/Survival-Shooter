#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$work = Join-Path $repo ('outputs/benchmark-series-check/test with spaces-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($work)
$fakeGame = Join-Path $work 'Fake Game.exe'
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /reference:System.Web.Extensions.dll "/out:$fakeGame" (Join-Path $PSScriptRoot 'FakeGame.cs')
if ($LASTEXITCODE -ne 0) { throw 'Cannot compile fake game.' }
$runner = Join-Path $PSScriptRoot '../Run-BenchmarkSeries.ps1'
$settings = Get-Content (Join-Path $PSScriptRoot '../series.example.json') -Raw | ConvertFrom-Json
$settings.gameExecutable = $fakeGame
$settings.presentMonPath = Join-Path $repo 'Tools/PresentMon/PresentMon-2.6.0-x64.exe'
$settings.replayPath = Join-Path $work 'replay.json'
$settings.resultsRoot = Join-Path $work 'results'
$settings.repetitions = 2
$settings.pauseSeconds = 0
$settings.timeoutSeconds = 10
$configPath = Join-Path $work 'config.json'
function Save-Config { $settings | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $configPath -Encoding UTF8 }
function Set-Mode($Mode) { @{duration=1;testMode=$Mode} | ConvertTo-Json | Set-Content -LiteralPath $settings.replayPath -Encoding UTF8 }
function Check($Condition, $Message) { if (!$Condition) { throw $Message } }
Set-Mode 'success'
Save-Config
& $runner -Config $configPath -ValidateOnly
Check (!(Test-Path $settings.resultsRoot)) 'ValidateOnly wrote results.'
# Validate the complete six-scenario plan without launching any processes.
& {
    $paired = Get-Content (Join-Path $PSScriptRoot '../series.pool-30s.json') -Raw | ConvertFrom-Json
    Check ($paired.gameExecutable -eq 'Survival Shooter.exe') 'Game path must be beside the configuration.'
    Check ($paired.presentMonPath -eq 'Tools/PresentMon/PresentMon-2.6.0-x64.exe') 'Incorrect portable PresentMon path.'
    $paired.gameExecutable = 'Fake Game.exe'
    $paired.presentMonPath = $settings.presentMonPath
    $paired.replayPath = $settings.replayPath
    $paired.resultsRoot = Join-Path $work 'paired-results'
    $pairedPath = Join-Path $work 'paired.json'
    $paired | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $pairedPath -Encoding UTF8
    . $runner -Config $pairedPath -ValidateOnly
    Check ($plan.Count -eq 72) 'Expected 72 paired measurements.'
    Check (!(Test-Path $paired.resultsRoot)) 'Paired ValidateOnly wrote results.'
    foreach ($allocation in @('Pool','InstantiateDestroy')) {
        foreach ($group in $paired.groups) {
            $positions = @()
            for ($repeat = 1; $repeat -le 6; $repeat++) {
                $pass = @($plan | Where-Object { $_.repetition -eq $repeat -and $_.allocation -eq $allocation })
                Check ($pass.Count -eq 6) 'Incomplete allocation pass.'
                for ($position = 0; $position -lt 6; $position++) {
                    if ($pass[$position].group.name -eq $group.name) { $positions += $position }
                }
            }
            Check ((($positions | Sort-Object) -join ',') -eq '0,1,2,3,4,5') 'Scenario positions are not balanced.'
        }
    }
    for ($repeat = 1; $repeat -le 6; $repeat++) {
        $runs = @($plan | Where-Object repetition -eq $repeat)
        foreach ($group in $paired.groups) {
            $measurements = @($runs | Where-Object { $_.group.name -eq $group.name })
            Check ($measurements.Count -eq 2) 'Scenario must run exactly twice per repetition.'
            Check (@($measurements.allocation | Select-Object -Unique).Count -eq 2) 'Scenario must run in both allocations.'
        }
        Check ($runs[0].allocation -eq 'InstantiateDestroy') 'Strict alternation must retain the starting allocation.'
    }
    for ($index = 1; $index -lt $plan.Count; $index++) {
        Check ($plan[$index].allocation -ne $plan[$index - 1].allocation) 'Adjacent allocations repeat, including repetition boundaries.'
        Check ($plan[$index].group.name -ne $plan[$index - 1].group.name) 'Adjacent scenarios repeat, including repetition boundaries.'
    }
    foreach ($group in $paired.groups) {
        $positions = @()
        for ($repeat = 1; $repeat -le 6; $repeat++) {
            $runs = @($plan | Where-Object repetition -eq $repeat)
            for ($index = 0; $index -lt $runs.Count; $index++) {
                if ($runs[$index].group.name -eq $group.name) { $positions += $index }
            }
        }
        Check ((($positions | Sort-Object) -join ',') -eq '0,1,2,3,4,5,6,7,8,9,10,11') 'Scenario must occupy each of the twelve run positions once.'
    }
}
& $runner -Config $configPath
$first = @(Get-ChildItem $settings.resultsRoot -Directory)[0]
$summary = @(Get-Content (Join-Path $first.FullName '_automation/summary.json') -Raw | ConvertFrom-Json)
Check ($summary.Count -eq 8) 'Wrong number of tests.'
Check (@($summary | Where-Object result -ne 'Completed').Count -eq 0) 'Success test failed.'
Check (@($summary | Where-Object { $_.screenWidth -ne 1920 -or $_.screenHeight -ne 1080 -or $_.fullscreen -or $_.qualityLevel -ne 'Ultra' }).Count -eq 0) 'Graphics settings missing from summary.'
Check (($summary.repetition -join ',') -eq '1,1,1,1,2,2,2,2') 'Repetitions do not run across all groups.'
Check (($summary.group -join ',') -eq 'Original-periodic,Original-periodic,Visual-periodic,Visual-periodic,Visual-periodic,Visual-periodic,Original-periodic,Original-periodic') 'Scenario pair order does not rotate between repetitions.'
Check (($summary.allocation -join ',') -eq 'InstantiateDestroy,Pool,InstantiateDestroy,Pool,Pool,InstantiateDestroy,Pool,InstantiateDestroy') 'Allocations do not alternate within repetitions or rotate their starting mode.'
Check (@(Get-ChildItem $first.FullName -Directory).Count -eq 3) 'Expected two group folders and _automation.'
$settings.repetitions = 3
$settings.graphics.quality = 'Very High'
$settings.graphics.fullscreen = $true
$settings.groups[0].allocations = @('InstantiateDestroy')
$settings.groups[1].allocations = @('Pool')
Save-Config
& $runner -Config $configPath
Check (@(Get-ChildItem $settings.resultsRoot -Directory).Count -eq 2) 'Second series reused folders.'
$second = @(Get-ChildItem $settings.resultsRoot -Directory | Where-Object FullName -ne $first.FullName)[0]
$rounds = @(Get-Content (Join-Path $second.FullName '_automation/summary.json') -Raw | ConvertFrom-Json)
Check (($rounds.allocation -join ',') -eq 'InstantiateDestroy,Pool,Pool,InstantiateDestroy,InstantiateDestroy,Pool') 'Single-allocation groups do not rotate across rounds.'
Check (($rounds.repetition -join ',') -eq '1,1,2,2,3,3') 'Incorrect repetition numbers for single-allocation groups.'
$settings.repetitions = 1
$settings.groups = @($settings.groups[0])
$settings.groups[0].allocations = @('InstantiateDestroy','Pool')
foreach ($mode in @('failed','missing','timeout','bad-count','bad-settings','bad-frame','bad-resolution','bad-quality')) {
    Set-Mode $mode
    $settings.timeoutSeconds = 2
    $settings.continueOnFailure = $mode -ne 'failed'
    Save-Config
    $before = @(Get-ChildItem $settings.resultsRoot -Directory).FullName
    $caught = $false
    try { & $runner -Config $configPath } catch { $caught = $true }
    Check $caught "Failure mode '$mode' was accepted."
    $directory = @(Get-ChildItem $settings.resultsRoot -Directory | Where-Object FullName -notin $before)[0]
    $rows = @(Get-Content (Join-Path $directory.FullName '_automation/summary.json') -Raw | ConvertFrom-Json)
    $expected = if ($settings.continueOnFailure) { 2 } else { 1 }
    Check ($rows.Count -eq $expected) "Incorrect continueOnFailure for '$mode'."
    Check (@($rows | Where-Object result -ne 'Failed').Count -eq 0) "Failure '$mode' not recorded."
}
Set-Mode 'success'
$validGroupName = $settings.groups[0].name
foreach ($invalidName in @('../escape', '_automation', 'CON', 'trailing.')) {
    $settings.groups[0].name = $invalidName
    Save-Config
    $caught = $false
    try { & $runner -Config $configPath -ValidateOnly } catch { $caught = $true }
    Check $caught "Invalid group name '$invalidName' was accepted."
}
$settings.groups[0].name = $validGroupName
$settings.graphics.width = 0
Save-Config
$caught = $false
try { & $runner -Config $configPath -ValidateOnly } catch { $caught = $true }
Check $caught 'Invalid graphics width was accepted.'
$settings.graphics.width = 1920
$settings.graphics.quality = 'Ultrra'
Save-Config
$caught = $false
try { & $runner -Config $configPath -ValidateOnly } catch { $caught = $true }
Check $caught 'Unknown graphics quality was accepted.'
& {
    $settings = Get-Content (Join-Path $PSScriptRoot '../series.profiler-300.json') -Raw | ConvertFrom-Json
    $settings.gameExecutable = $fakeGame
    $settings.presentMonPath = Join-Path $repo 'Tools/PresentMon/PresentMon-2.6.0-x64.exe'
    $settings.replayPath = Join-Path $work 'profiler-replay.json'
    $settings.resultsRoot = Join-Path $work 'profiler-results'
    $settings.pauseSeconds = 0
    $settings.timeoutSeconds = 10
    $configPath = Join-Path $work 'profiler-config.json'
    @{duration=30;testMode='success'} | ConvertTo-Json | Set-Content -LiteralPath $settings.replayPath -Encoding UTF8
    Save-Config
    . $runner -Config $configPath -ValidateOnly
    Check ($plan.Count -eq 4) 'Expected four diagnostic runs by default.'
    foreach ($group in $settings.groups) {
        foreach ($allocation in @('Pool','InstantiateDestroy')) {
            Check (@($plan | Where-Object { $_.group.name -eq $group.name -and $_.allocation -eq $allocation }).Count -eq 1) 'Each diagnostic condition must run once by default.'
        }
    }
    $settings.repetitions = 1
    Save-Config
    & $runner -Config $configPath
    $series = @(Get-ChildItem $settings.resultsRoot -Directory)[0]
    $resultRows = @(Get-Content (Join-Path $series.FullName '_automation/summary.json') -Raw | ConvertFrom-Json)
    Check ($resultRows.Count -eq 4) 'Expected four diagnostic runs in orchestration test.'
    Check (@($resultRows | Where-Object result -ne 'Completed').Count -eq 0) 'Diagnostic orchestration failed.'
    Check (@($resultRows | Where-Object { !(Test-Path (Join-Path $_.profilerDirectory 'profiler.json')) }).Count -eq 0) 'Profiler paths not retained in summary.'
    Check (@($resultRows | Where-Object { ![string]::IsNullOrEmpty($_.captureDirectory) }).Count -eq 0) 'Profiler diagnostics must not record a PresentMon capture.'
    $settings.continueOnFailure = $false
    foreach ($mode in @('missing-profiler','empty-profiler','oversized-profiler','bad-profiler-counters','dropped-profiler','stalled-profiler')) {
        @{duration=30;testMode=$mode} | ConvertTo-Json | Set-Content -LiteralPath $settings.replayPath -Encoding UTF8
        Save-Config
        $caught = $false
        try { & $runner -Config $configPath } catch { $caught = $true }
        Check $caught "Invalid diagnostic capture '$mode' was accepted."
    }
    @{duration=31;testMode='success'} | ConvertTo-Json | Set-Content -LiteralPath $settings.replayPath -Encoding UTF8
    Save-Config
    $caught = $false
    try { & $runner -Config $configPath -ValidateOnly } catch { $caught = $true }
    Check $caught 'Diagnostic replay duration was not checked.'
}
& {
    $settings = Get-Content (Join-Path $PSScriptRoot '../series.shadows-culling-30.json') -Raw | ConvertFrom-Json
    $settings.gameExecutable = $fakeGame
    $settings.presentMonPath = Join-Path $repo 'Tools/PresentMon/PresentMon-2.6.0-x64.exe'
    $settings.replayPath = Join-Path $work 'shadow-replay.json'
    $settings.resultsRoot = Join-Path $work 'shadow-results'
    $settings.pauseSeconds = 0
    $configPath = Join-Path $work 'shadow-config.json'
    Set-Mode 'success'
    Save-Config
    . $runner -Config $configPath -ValidateOnly
    Check ($plan.Count -eq 30) 'Expected thirty shadow/culling runs.'
    Check (!(Test-Path $settings.resultsRoot)) 'Shadow ValidateOnly wrote results.'
    Check (@($settings.groups.scene | Select-Object -Unique).Count -eq 1) 'Shadow comparison must use the same scene.'
    foreach ($group in $settings.groups) {
        Check (@($plan | Where-Object { $_.group.name -eq $group.name }).Count -eq 5) 'Each shadow/culling combination must run five times.'
    }
    $settings.repetitions = 1
    $settings.groups = @($settings.groups[0], $settings.groups[1])
    Save-Config
    & $runner -Config $configPath
    $series = @(Get-ChildItem $settings.resultsRoot -Directory)[0]
    $rows = @(Get-Content (Join-Path $series.FullName '_automation/summary.json') -Raw | ConvertFrom-Json)
    Check (($rows.mainLightShadows -join ',') -eq 'Soft,Off') 'Shadow states missing from summary.'
    foreach ($mode in @('bad-shadows','missing-shadows')) {
        Set-Mode $mode
        Save-Config
        $caught = $false
        try { & $runner -Config $configPath } catch { $caught = $true }
        Check $caught "Shadow failure '$mode' was accepted."
    }
    foreach ($invalid in @('On','soft',$null)) {
        $settings.groups[0].mainLightShadows = $invalid
        Save-Config
        $caught = $false
        try { & $runner -Config $configPath -ValidateOnly } catch { $caught = $true }
        Check $caught 'Invalid shadow mode was accepted.'
    }
}
Write-Host 'PASS: planning, balanced ordering, graphics, shadows and frame validation, failure handling, and segmented Profiler orchestration.'
& {
    $settings = Get-Content (Join-Path $PSScriptRoot '../series.profiler-cpu-rendering.json') -Raw | ConvertFrom-Json
    $settings.gameExecutable = $fakeGame
    $settings.replayPath = Join-Path $work 'cpu-rendering-replay.json'
    $settings.resultsRoot = Join-Path $work 'cpu-rendering-results'
    $settings.pauseSeconds = 0
    $configPath = Join-Path $work 'cpu-rendering-config.json'
    @{duration=30;testMode='success'} | ConvertTo-Json | Set-Content -LiteralPath $settings.replayPath -Encoding UTF8
    Save-Config
    . $runner -Config $configPath -ValidateOnly
    Check ($plan.Count -eq 6) 'Expected six CPU/rendering diagnostic runs.'
    Check (!(Test-Path $settings.resultsRoot)) 'CPU/rendering ValidateOnly wrote results.'
    Check (@($plan | Where-Object { $_.group.scenario -ne 'BenchmarkGrouped' -or $_.allocation -ne 'Pool' }).Count -eq 0) 'CPU/rendering conditions differ from the measured series.'
    & $runner -Config $configPath
    $series = @(Get-ChildItem $settings.resultsRoot -Directory)[0]
    $rows = @(Get-Content (Join-Path $series.FullName '_automation/summary.json') -Raw | ConvertFrom-Json)
    Check ($rows.Count -eq 6 -and @($rows | Where-Object result -ne 'Completed').Count -eq 0) 'CPU/rendering orchestration failed.'
    $settings.groups = @($settings.groups[0])
    foreach ($mode in @('missing-profiler-settings','bad-profiler-culling','bad-profiler-shadows','bad-profiler-counters')) {
        Set-Mode $mode
        # Profiler diagnostics require 30 seconds even for failure cases.
        @{duration=30;testMode=$mode} | ConvertTo-Json | Set-Content -LiteralPath $settings.replayPath -Encoding UTF8
        Save-Config
        $caught = $false
        try { & $runner -Config $configPath } catch { $caught = $true }
        Check $caught "Invalid CPU/rendering profile '$mode' was accepted."
    }
}
$shadowExecutable = Join-Path $work 'ShadowOverrideTests.exe'
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo "/out:$shadowExecutable" (Join-Path $repo 'Assets/Scripts/Manager/BenchmarkShadowOverride.cs') (Join-Path $PSScriptRoot 'ShadowOverrideTests.cs')
Check ($LASTEXITCODE -eq 0) 'Cannot compile shadow lifecycle tests.'
& $shadowExecutable | Out-Host
Check ($LASTEXITCODE -eq 0) 'Shadow lifecycle tests failed.'
$captureSource = Join-Path $repo 'Assets/Scripts/Manager/BenchmarkProfilerCapture.cs'
$captureTests = Join-Path $PSScriptRoot 'ProfilerCaptureTests.cs'
foreach ($variant in @('Development','Release')) {
    $testExecutable = Join-Path $work "Profiler-$variant.exe"
    $compileArgs = @('/nologo', '/reference:System.Web.Extensions.dll', "/out:$testExecutable", $captureSource, $captureTests)
    if ($variant -eq 'Development') { $compileArgs += '/define:DEVELOPMENT_BUILD' }
    & "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" @compileArgs
    Check ($LASTEXITCODE -eq 0) 'Cannot compile Profiler lifecycle test.'
    & $testExecutable | Out-Host
    Check ($LASTEXITCODE -eq 0) 'Profiler must stay disabled without its opt-in flag.'
    & $testExecutable --benchmark-profiler-log --benchmark-profiler-directory (Join-Path $work "capture-$variant") | Out-Host
    Check ($LASTEXITCODE -eq 0) 'Profiler lifecycle test failed.'
}
