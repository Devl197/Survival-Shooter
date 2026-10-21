#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Config = (Join-Path $PSScriptRoot 'series.example.json'),
    [switch]$ValidateOnly
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Properties($Object, [string[]]$Names, [string]$Context, [string[]]$Optional = @()) {
    if ($null -eq $Object) { throw "Missing $Context" }
    foreach ($property in $Object.PSObject.Properties.Name) {
        if ($Names -notcontains $property -and $Optional -notcontains $property) { throw "Unknown property '$property' in $Context" }
    }
    foreach ($name in $Names) {
        if ($null -eq $Object.PSObject.Properties[$name]) { throw "Missing '$name' in $Context" }
    }
}
function Assert-Name([string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Name) -or $Name.Length -gt 80 -or
        $Name -ne $Name.Trim() -or $Name.EndsWith('.') -or
        $Name -match '[<>:"/\\|?*\x00-\x1f]' -or $Name -in @('.', '..') -or
        $Name -match '^(CON|PRN|AUX|NUL|COM[1-9\u00b9\u00b2\u00b3]|LPT[1-9\u00b9\u00b2\u00b3])(\.|$)') {
        throw "Invalid folder name: '$Name'"
    }
}
function Assert-Number($Value, [double]$Minimum, [double]$Maximum, [bool]$Integer, [string]$Name) {
    if ($Value -is [bool] -or $Value -is [string] -or $null -eq $Value) { throw "Invalid number: $Name" }
    $number = [double]$Value
    if ([double]::IsNaN($number) -or [double]::IsInfinity($number) -or $number -lt $Minimum -or
        $number -gt $Maximum -or ($Integer -and $number -ne [math]::Floor($number))) { throw "Invalid number: $Name" }
}
function Resolve-ConfigPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { throw 'Empty path in configuration.' }
    $expanded = [Environment]::ExpandEnvironmentVariables($Path)
    if (![IO.Path]::IsPathRooted($expanded)) { $expanded = Join-Path $configDirectory $expanded }
    return [IO.Path]::GetFullPath($expanded)
}
function Write-Json($Value, [string]$Path) {
    $json = ConvertTo-Json -InputObject $Value -Depth 12
    [IO.File]::WriteAllText($Path, $json, (New-Object Text.UTF8Encoding($false)))
}

$configPath = (Resolve-Path -LiteralPath $Config).Path
$configDirectory = Split-Path -Parent $configPath
$settings = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
Assert-Properties $settings @('seriesName','gameExecutable','presentMonPath','replayPath','resultsRoot',
    'repetitions','pauseSeconds','timeoutSeconds','continueOnFailure','graphics','groups') 'series' @('unityProfiler')
$unityProfiler = $false
if ($null -ne $settings.PSObject.Properties['unityProfiler']) {
    if ($settings.unityProfiler -isnot [bool]) { throw 'unityProfiler must be a JSON boolean.' }
    $unityProfiler = $settings.unityProfiler
}
Assert-Name $settings.seriesName
Assert-Number $settings.repetitions 1 10000 $true 'repetitions'
Assert-Number $settings.pauseSeconds 0 3600 $false 'pauseSeconds'
Assert-Number $settings.timeoutSeconds 1 86400 $true 'timeoutSeconds'
if ($settings.continueOnFailure -isnot [bool]) { throw 'continueOnFailure must be a JSON boolean.' }
Assert-Properties $settings.graphics @('width','height','fullscreen','quality') 'graphics'
Assert-Number $settings.graphics.width 320 16384 $true 'graphics.width'
Assert-Number $settings.graphics.height 200 16384 $true 'graphics.height'
if ($settings.graphics.fullscreen -isnot [bool]) { throw 'graphics.fullscreen must be a JSON boolean.' }
if ($settings.graphics.quality -cnotin @('Very Low','Low','Medium','High','Very High','Ultra')) {
    throw 'Unknown graphics.quality. Use a quality name defined in this project.'
}
$game = Resolve-ConfigPath $settings.gameExecutable
$presentMon = Resolve-ConfigPath $settings.presentMonPath
$replay = Resolve-ConfigPath $settings.replayPath
$resultsRoot = Resolve-ConfigPath $settings.resultsRoot
$requiredFiles = @($game, $replay)
if (!$unityProfiler) { $requiredFiles += $presentMon }
foreach ($file in $requiredFiles) {
    if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw "File not found: $file" }
    if ($file.Contains('"')) { throw "Unsupported quote in path: $file" }
}
$replayData = Get-Content -LiteralPath $replay -Raw | ConvertFrom-Json
Assert-Number $replayData.duration 0.001 86400 $false 'replay.duration'
if ($unityProfiler -and [math]::Abs($replayData.duration - 30) -gt 0.0001) {
    throw 'The diagnostic Profiler series requires a 30-second replay.'
}
if (!$unityProfiler -and (Get-FileHash -LiteralPath $presentMon -Algorithm SHA256).Hash -ne
    'B2A706BC6AD475749E3B7E3409263AA1E6906D45BDCF993F6DBC0F660188F1AF') {
    throw 'PresentMon must be the pinned 2.6.0 executable.'
}
if ($settings.groups -isnot [array] -or $settings.groups.Count -eq 0) { throw 'groups must be a nonempty JSON array.' }
$names = @{}
$plan = @()
foreach ($group in $settings.groups) {
    Assert-Properties $group @('name','scene','scenario','allocations','periodicPathUpdates',
        'pathUpdateIntervalSeconds','animatorCulling','updateWhenOffscreen','skinnedMotionVectors') 'group'
    Assert-Name $group.name
    if ($group.name -eq '_automation') { throw 'The group name _automation is reserved for series logs.' }
    if ($names.ContainsKey($group.name)) { throw "Duplicate group: $($group.name)" }
    $names[$group.name] = $true
    if ($group.scene -notin @('Level_01','Level_01_VisualExperiment')) { throw "Unknown scene: $($group.scene)" }
    if ($group.scenario -notin @('BenchmarkContinuous50','BenchmarkBurst50',
        'BenchmarkContinuous100','BenchmarkBurst100','BenchmarkContinuous','BenchmarkBurst',
        'BenchmarkFrequent','BenchmarkGrouped')) {
        throw "Unknown scenario: $($group.scenario)"
    }
    if ($unityProfiler -and $group.scenario -notin @('BenchmarkContinuous','BenchmarkBurst')) {
        throw 'This diagnostic Profiler series supports Continuous 300 and Burst 300 only.'
    }
    if ($group.animatorCulling -notin @('AlwaysAnimate','CullUpdateTransforms','CullCompletely')) { throw 'Unknown animatorCulling.' }
    Assert-Number $group.pathUpdateIntervalSeconds 0.01 86400 $false 'pathUpdateIntervalSeconds'
    foreach ($flag in @('periodicPathUpdates','updateWhenOffscreen','skinnedMotionVectors')) {
        if ($group.$flag -isnot [bool]) { throw "$flag must be a JSON boolean." }
    }
    if ($group.allocations -isnot [array] -or $group.allocations.Count -eq 0 -or
        @($group.allocations | Select-Object -Unique).Count -ne $group.allocations.Count) { throw 'allocations must be a nonempty array without duplicates.' }
    foreach ($allocation in $group.allocations) {
        if ($allocation -cnotin @('Pool','InstantiateDestroy')) { throw "Unknown allocation: $allocation" }
    }
}
# Offset the second allocation stream so both scenario and allocation change.
# Small or mixed-allocation series retain paired rotation as a fallback.
$interleaveScenarios = $settings.groups.Count -ge 4 -and $settings.groups[0].allocations.Count -eq 2
foreach ($group in $settings.groups) {
    if (($group.allocations -join ',') -ne ($settings.groups[0].allocations -join ',')) {
        $interleaveScenarios = $false
    }
}
$scenarioOffset = [math]::Max(3, [math]::Floor($settings.groups.Count / 2))
for ($repeat = 0; $repeat -lt $settings.repetitions; $repeat++) {
    for ($position = 0; $position -lt $settings.groups.Count; $position++) {
        $baseGroup = $settings.groups[($position + $repeat) % $settings.groups.Count]
        for ($mode = 0; $mode -lt $baseGroup.allocations.Count; $mode++) {
            $group = $baseGroup
            $allocationIndex = ($mode + $repeat) % $group.allocations.Count
            if ($interleaveScenarios) {
                $group = $settings.groups[($position + $repeat + $mode * $scenarioOffset) % $settings.groups.Count]
                $allocationIndex = $mode
            }
            $plan += [pscustomobject]@{ group = $group; repetition = $repeat + 1
                allocation = $group.allocations[$allocationIndex] }
        }
    }
}
$plan | Select-Object @{n='Group';e={$_.group.name}}, repetition, allocation | Format-Table | Out-Host
Write-Host ("Planned runs: {0}; results root: {1}" -f $plan.Count, $resultsRoot)
Write-Host ("Unity Profiler segmented recording: {0}" -f $unityProfiler)
if ($unityProfiler) { Write-Host 'Diagnostic mode: Unity Profiler only, 60 FPS, no PresentMon process.' }
Write-Host ('Graphics for every run: {0}x{1}; fullscreen={2}; quality={3}' -f $settings.graphics.width,
    $settings.graphics.height, $settings.graphics.fullscreen, $settings.graphics.quality)
if ($ValidateOnly) { return }

$seriesId = '{0}-{1}-{2}' -f $settings.seriesName, [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'), [Guid]::NewGuid().ToString('N')
$seriesDirectory = Join-Path $resultsRoot $seriesId
$controlDirectory = Join-Path $seriesDirectory '_automation'
[void][IO.Directory]::CreateDirectory($controlDirectory)
Write-Json $settings (Join-Path $controlDirectory 'series.json')
foreach ($group in $settings.groups) { [void][IO.Directory]::CreateDirectory((Join-Path $seriesDirectory $group.name)) }
$summary = @()
$process = $null
try {
    for ($index = 0; $index -lt $plan.Count; $index++) {
        $item = $plan[$index]
        $group = $item.group
        $runId = 'run-{0:D4}' -f ($index + 1)
        $requestPath = Join-Path $controlDirectory "$runId.request.json"
        $reportPath = Join-Path $controlDirectory "$runId.report.json"
        $logPath = Join-Path $controlDirectory "$runId.player.log"
        $groupDirectory = Join-Path $seriesDirectory $group.name
        $profilerDirectory = if ($unityProfiler) { Join-Path $groupDirectory "$runId-profiler" } else { '' }
        $request = [ordered]@{ scene = $group.scene; scenario = $group.scenario; allocation = $item.allocation
            replayPath = $replay; presentMonPath = $presentMon; resultsDirectory = $groupDirectory; reportPath = $reportPath
            periodicPathUpdates = $group.periodicPathUpdates; pathUpdateIntervalSeconds = $group.pathUpdateIntervalSeconds
            animatorCulling = $group.animatorCulling; updateWhenOffscreen = $group.updateWhenOffscreen
            skinnedMotionVectors = $group.skinnedMotionVectors; graphics = $settings.graphics }
        $request.captureWithPresentMon = !$unityProfiler
        Write-Json $request $requestPath
        $row = [pscustomobject][ordered]@{ run = $runId; group = $group.name; scene = $group.scene
            scenario = $group.scenario; allocation = $item.allocation; repetition = $item.repetition
            screenWidth = $settings.graphics.width; screenHeight = $settings.graphics.height
            fullscreen = $settings.graphics.fullscreen; qualityLevel = $settings.graphics.quality
            result = 'Running'; error = ''; captureDirectory = ''; profilerDirectory = $profilerDirectory; exitCode = $null
            startedUtc = [DateTime]::UtcNow.ToString('o'); endedUtc = ''; reportPath = $reportPath; logPath = $logPath }
        $summary += $row
        Write-Json @($summary) (Join-Path $controlDirectory 'summary.json')
        Write-Host ("[{0}/{1}] {2} / {3}, repetition {4}" -f ($index + 1), $plan.Count, $group.name, $item.allocation, $item.repetition)
        try {
            # The rendered game needs a visible window and focus for PresentMon measurements.
            $arguments = '--benchmark-request "{0}" -logFile "{1}" -screen-width {2} -screen-height {3} -screen-fullscreen {4} -screen-quality "{5}"' -f $requestPath, $logPath,
                $settings.graphics.width, $settings.graphics.height, [int]$settings.graphics.fullscreen, $settings.graphics.quality
            if ($unityProfiler) {
                $arguments += ' --benchmark-profiler-log --benchmark-profiler-directory "{0}"' -f $profilerDirectory
            }
            $process = Start-Process -FilePath $game -ArgumentList $arguments -WorkingDirectory (Split-Path -Parent $game) -PassThru
            $deadline = [DateTime]::UtcNow.AddSeconds($settings.timeoutSeconds)
            while (!$process.HasExited -and [DateTime]::UtcNow -lt $deadline) {
                Start-Sleep -Milliseconds 250
                $process.Refresh()
            }
            if (!$process.HasExited) {
                Stop-Process -Id $process.Id -Force
                $process.WaitForExit()
                throw 'Timeout: the game did not exit before timeoutSeconds.'
            }
            $process.WaitForExit()
            $row.exitCode = $process.ExitCode
            if (!(Test-Path -LiteralPath $reportPath)) { throw 'Missing automation report. Rebuild the game with automation support; see player log.' }
            $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
            $row.result = $report.result
            $row.captureDirectory = $report.captureDirectory
            if ($report.result -ne 'Completed' -or $process.ExitCode -ne 0) { throw "Game result: $($report.result). $($report.error)" }
            if (!$unityProfiler) {
                $capturePath = [IO.Path]::GetFullPath($report.captureDirectory)
                if (!(Split-Path -Parent $capturePath).Equals($groupDirectory, [StringComparison]::OrdinalIgnoreCase)) {
                    throw 'Capture directory is outside its configured group.'
                }
                $metadata = Get-Content -LiteralPath (Join-Path $capturePath 'session.json') -Raw | ConvertFrom-Json
                foreach ($field in @('screenWidth','screenHeight','fullscreen','qualityLevel')) {
                    if ($null -eq $metadata.PSObject.Properties[$field]) {
                        throw 'Capture is missing graphics metadata. Rebuild the game with graphics configuration support.'
                    }
                }
                if ($metadata.result -ne 'Completed' -or $metadata.filteredFrames -le 0 -or
                    $metadata.scene -ne $request.scene -or $metadata.scenario -ne $request.scenario -or
                    $metadata.allocation -ne $request.allocation -or $metadata.processId -ne $process.Id -or
                    $metadata.replayFile -ne $request.replayPath -or !$metadata.enemyCullingOverridden -or
                    $metadata.animatorCullingMode -ne $request.animatorCulling -or
                    $metadata.periodicPathUpdates -ne $request.periodicPathUpdates -or
                    [math]::Abs($metadata.pathUpdateIntervalSeconds - $request.pathUpdateIntervalSeconds) -gt 0.00001 -or
                    $metadata.updateWhenOffscreen -ne $request.updateWhenOffscreen -or
                    $metadata.skinnedMotionVectors -ne $request.skinnedMotionVectors -or
                    $metadata.screenWidth -ne $settings.graphics.width -or $metadata.screenHeight -ne $settings.graphics.height -or
                    $metadata.fullscreen -ne $settings.graphics.fullscreen -or $metadata.qualityLevel -cne $settings.graphics.quality) {
                    throw 'Capture metadata is invalid or does not match the request.'
                }
                $measured = Join-Path $capturePath 'measured.csv'
                $frames = 0
                Import-Csv -LiteralPath $measured | ForEach-Object {
                    $frameTime = [double]::Parse($_.FrameTime, [Globalization.CultureInfo]::InvariantCulture)
                    if ([double]::IsNaN($frameTime) -or [double]::IsInfinity($frameTime) -or $frameTime -le 0) { throw 'Invalid measured FrameTime.' }
                    $frames++
                }
                if ($frames -ne $metadata.filteredFrames) { throw 'Measured frame count does not match session.json.' }
            }
            if ($unityProfiler) {
                if (![string]::IsNullOrEmpty($report.captureDirectory)) {
                    throw 'Diagnostic build unexpectedly started PresentMon. Rebuild with Profiler-only support.'
                }
                if (Select-String -LiteralPath $logPath -Pattern 'Skipping profile frame|Receiver can not keep up' -Quiet) {
                    throw 'Unity dropped Profiler frames. This capture is incomplete; inspect the player log.'
                }
                $profile = Get-Content -LiteralPath (Join-Path $profilerDirectory 'profiler.json') -Raw | ConvertFrom-Json
                if ($profile.result -ne 'Completed' -or $profile.scenario -ne $request.scenario -or
                    $profile.allocation -ne $request.allocation -or @($profile.segments).Count -eq 0 -or
                    $profile.framesPerSegment -ne 200 -or $profile.removed -ne 120 -or $profile.spawned -ne 420 -or
                    $profile.poolExpansions -ne 0 -or $profile.targetFrameRate -ne 60 -or
                    $profile.bufferBytes -ne 134217728 -or $profile.captureWithPresentMon) {
                    throw 'Profiler capture is incomplete or does not match the diagnostic scenario.'
                }
                if ($profile.segments[-1].endSeconds -gt 32) {
                    throw 'Profiler capture exceeded 32 wall-clock seconds for a 30-second replay. Recording stalled.'
                }
                foreach ($segment in $profile.segments) {
                    if ([IO.Path]::GetFileName($segment.file) -cne $segment.file -or $segment.file -notmatch '^segment-\d{4,}\.raw$') {
                        throw 'Invalid Profiler segment filename.'
                    }
                    $segmentPath = Join-Path $profilerDirectory $segment.file
                    if (!(Test-Path -LiteralPath $segmentPath -PathType Leaf) -or (Get-Item -LiteralPath $segmentPath).Length -eq 0 -or
                        $segment.endFrame -lt $segment.startFrame -or $segment.endFrame - $segment.startFrame -gt 200) {
                        throw 'Missing, empty or oversized Profiler segment.'
                    }
                }
            }
        }
        catch { $row.result = 'Failed'; $row.error = $_.Exception.Message; Write-Warning $row.error }
        finally {
            if ($row.result -eq 'Running') { $row.result = 'Aborted'; $row.error = 'Series execution was interrupted.' }
            if ($null -ne $process) {
                if (!$process.HasExited) { Stop-Process -Id $process.Id -Force; $process.WaitForExit() }
                $process.Dispose(); $process = $null
            }
            $row.endedUtc = [DateTime]::UtcNow.ToString('o')
            Write-Json @($summary) (Join-Path $controlDirectory 'summary.json')
            $summary | Export-Csv -LiteralPath (Join-Path $controlDirectory 'summary.csv') -NoTypeInformation -Encoding UTF8
        }
        if ($row.result -ne 'Completed' -and !$settings.continueOnFailure) { break }
        if ($index -lt $plan.Count - 1 -and $settings.pauseSeconds -gt 0) {
            Start-Sleep -Milliseconds ([int]($settings.pauseSeconds * 1000))
        }
    }
}
finally {
    if ($null -ne $process) {
        if (!$process.HasExited) { Stop-Process -Id $process.Id -Force }
        $process.Dispose()
    }
    Write-Host "Series results: $seriesDirectory"
}
if (@($summary | Where-Object result -ne 'Completed').Count -gt 0) { throw 'Some benchmarks failed. See _automation/summary.csv and player logs.' }
