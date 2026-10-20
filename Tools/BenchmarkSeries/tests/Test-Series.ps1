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
& $runner -Config $configPath
$first = @(Get-ChildItem $settings.resultsRoot -Directory)[0]
$summary = @(Get-Content (Join-Path $first.FullName '_automation/summary.json') -Raw | ConvertFrom-Json)
Check ($summary.Count -eq 8) 'Wrong number of tests.'
Check (@($summary | Where-Object result -ne 'Completed').Count -eq 0) 'Success test failed.'
Check (@($summary | Where-Object { $_.screenWidth -ne 1920 -or $_.screenHeight -ne 1080 -or $_.fullscreen -or $_.qualityLevel -ne 'Ultra' }).Count -eq 0) 'Graphics settings missing from summary.'
Check (($summary.repetition -join ',') -eq '1,1,1,1,2,2,2,2') 'Repetitions do not run across all groups.'
Check (($summary.group -join ',') -eq 'Original-periodic,Original-periodic,Visual-periodic,Visual-periodic,Original-periodic,Original-periodic,Visual-periodic,Visual-periodic') 'Groups do not run in configuration order within each round.'
Check (($summary.allocation -join ',') -eq 'InstantiateDestroy,Pool,InstantiateDestroy,Pool,Pool,InstantiateDestroy,Pool,InstantiateDestroy') 'Allocations do not alternate within each group.'
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
Check (($rounds.allocation -join ',') -eq 'InstantiateDestroy,Pool,InstantiateDestroy,Pool,InstantiateDestroy,Pool') 'Single-allocation groups do not alternate across rounds.'
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
Write-Host 'PASS: planning, paths with spaces, isolation, round-robin groups, graphics arguments and metadata, failures, timeout, CSV validation and stop/continue policies.'
