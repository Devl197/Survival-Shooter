# PresentMon 2.6.0 integration

Downloaded from the official GameTechDev/PresentMon GitHub release:
https://github.com/GameTechDev/PresentMon/releases/tag/v2.6.0

Executable: PresentMon-2.6.0-x64.exe
SHA-256: b2a706bc6ad475749e3b7e3409263aa1e6906d45bdcf993f6dbc0f660188f1af
The digest was checked against the GitHub release asset digest after download.
LICENSE.txt and README-upstream.md are from the same tag. No MSI/service was installed.
The bridge verifies this executable digest before every capture. It never elevates
itself, modifies user groups, or terminates unrelated ETW sessions.

## Setup (one Windows build run)

The build now starts in Assets/Scenes/BenchmarkMenu.unity, followed by Level_01.
Use Play normal game to load normal gameplay with waves and power-ups. This explicitly
disables BenchmarkRunner and replay startup, regardless of the scene's saved benchmark
settings. It requires neither a replay nor PresentMon and keeps normal frame settings.
Build once with both scenes enabled in that order. The menu lists all four scenario
assets and lets each run select Instantiate/Destroy or Pool without rebuilding.
It lists JSON files in Application.persistentDataPath/Replays. Select a file, change
the folder and Refresh, or enter an absolute replay file path directly. The menu
validates the recording against Level_01 before loading gameplay. Duration comes
from that recording. Menu launches always use Playback and never overwrite recordings.

Capture with PresentMon is on by default. Copy Tools/PresentMon beside the game exe
or enter an absolute executable path in the menu. Disable capture to exercise the
menu in the Unity Editor. Click Start benchmark to load gameplay; menu/scene loading
and pool preparation remain outside the measured interval. Restart the application
for each comparison run and choose the same scenario/replay with the other allocation.
Results go to Application.persistentDataPath/Benchmarks in unique run folders.
Set Results subfolder in the menu to group captures under a category, for example
Benchmarks/AI-tests/<unique-run>. The category is created automatically at launch,
and the menu remembers it. Leave it empty to save directly under Benchmarks.
Enter a single folder name; paths and reserved Windows names are rejected.
There is no automatic repeat or return to menu after completion. Esc exits the build.

Opening Level_01 directly in the Editor still uses its Inspector settings, including
Record mode for making new recordings. The menu overrides apply only to the next
gameplay scene initialization and do not change saved assets or scene settings.

For direct scene testing without the menu:

1. Configure BenchmarkRunner as usual: Run Benchmark, scenario, allocation and
   SessionReplay Record/Playback. Playback is recommended for performance comparison.
2. Enable Capture With Present Mon on BenchmarkRunner before building.
3. Set Present Mon Path to an absolute executable path, or copy Tools/PresentMon
   next to the built game's exe. Relative paths resolve against the directory
   containing the game exe (parent of Application.dataPath).
4. Run the Windows build with focus. Editor captures are rejected deliberately.
5. If ETW reports access denied, use an account with Performance Log Users access
   or run with the necessary Windows privileges, following upstream documentation.
   The game records the failure instead of silently running an unmeasured benchmark.

No automatic repeats. Existing benchmarks work without PresentMon when its checkbox
is off. The executable is not automatically copied by Unity's build pipeline.

## Uncapped benchmark and replacement scenarios

BenchmarkRunner temporarily sets QualitySettings.vSyncCount to 0 and
Application.targetFrameRate to -1 before preparation, with or without PresentMon.
Previous values are restored after finalization, failure or component disable.
Normal gameplay and saved Quality settings are unchanged. External driver/RTSS limits
must be disabled separately. New session.json files record vSyncCount and targetFrameRate.

Select a scenario asset from Assets/Scripts/ScriptableObjects in BenchmarkRunner:

| Asset | Pattern | Population | Group | Interval | Replacement load |
| --- | --- | --- | --- | --- | --- |
| BenchmarkContinuous | Continuous | 300 | 10 | 2 s | 5/s |
| BenchmarkFrequent | Continuous | 300 | 30 | 0.5 s | 60/s, spread across the interval |
| BenchmarkGrouped | Burst | 300 | 60 | 1 s | 60 together each second |

The two new scenarios retain seed 12345, equal type weights, initial lifetime 5 s
and death duration 2 s. First deaths start at 5 s; replacements start at 7 s.
Duration comes from the replay; replacements that cannot finish before its end are
excluded. Assign the scenario explicitly; existing scene selections are preserved.
Compare InstantiateDestroy and Pool using the same replay and graphics settings.
These are proposed stress workloads, not a guarantee that pooling improves FPS.

## Timing and output

After schedule/pool/initial population preparation, simulation is held at timeScale 0
and manual movement is blocked. PresentMon is launched hidden for this game's PID,
with a unique session name, --v2_metrics, --qpc_time and CSV stdout redirected to raw.csv.
Readiness means at least one parseable CPUStartQPC row from the target PID, not merely
successful process creation. Timeout defaults to 30 real seconds. A one-second frozen
stabilization interval follows readiness, then replay initialization and the start
QPC marker. This is renderer stabilization, not a simulation/AI warm-up.

The end QPC marker is taken after the last replay step, before replay saving and
cleanup. Cleanup is delayed until capture finalization. A two-second real-time tail
collects in-flight events before a named-session graceful stop. F8 ends with Stopped;
Esc/application shutdown/component disable mark Aborted. Pause, focus loss or premature
capture exit invalidate a measured run. Do not pause or change timeScale during tests.

Output: Application.persistentDataPath/Benchmarks/<UTC-time>-<unique-id>/
- raw.csv: complete unfiltered PresentMon output.
- session.json: QPC start/end/frequency, process/session IDs, scenario, allocation,
  active scene name (`scene`, for example Level_01 or Level_01_VisualExperiment),
  configuration, duration, tool hash/version and final result. Includes
  `periodicPathUpdates` (boolean) and `pathUpdateIntervalSeconds` (configured
  interval in seconds, also recorded when disabled), sampled from spawned enemies
  before capture. Do not change AI settings during a measured run. Missing fields
  in older reports mean unknown settings.
  Reports created before scene recording was added have no `scene` field; the
  replay file's scene is not a reliable substitute because both variants accept
  the same replay.
- capture.log: PresentMon diagnostics, including permissions errors.
- measured.csv: only successful runs, filtered CPU frame intervals.
- invalid-partial.csv: partial filter output if filtering failed; NOT usable results.

Filtering requires CPUStartQPC, FrameTime, ProcessID and SwapChainAddress from the
pinned v2 schema. Keep only the target PID and full CPU intervals satisfying
startQpc <= CPUStartQPC and CPUStartQPC + FrameTime*frequency/1000 <= endQpc.
Boundary-crossing intervals are excluded. Multiple swap chains cause a failure rather
than mixing unrelated frame streams. Decimal arithmetic preserves large QPC values.
Other columns are preserved, but this containment rule is for CPU frame intervals;
display/GPU timing analysis requires its own interval policy. No summary statistics
or p95/p99 report is generated yet.

Raw data remains available for audit. The capture bridge itself adds per-frame
stdout handling and disk-write overhead, equally in both allocation modes. Preparation,
raw capture and finalization are intentionally visible outside measured.csv. Run
Release builds with identical settings; use the Unity Profiler separately for diagnosis.
On a stalled system, a frame can cover multiple scheduled events; they are not dropped.

## Verification

- Verified official asset hash and executable --help version/options.
- A one-second ETW session successfully started and stopped outside the sandbox.
  The target was a non-rendering test process, so this did not validate game frames.
- All game scripts compile against Unity 2018.3.3f1 libraries.
- tests/CaptureTests.cs exercises complete interval containment, wrong PID rejection,
  CSV quoting, schema mismatch, multi-swap-chain rejection, large QPC precision and
  disposal before process startup. Compile it together with PresentMonCapture.cs;
  its JsonUtility stub is only for this standalone test, never place it under Assets.
- End-to-end graphical build capture remains to be verified locally: compare successful
  pool/non-pool sessions, then F8, Esc, pause, focus loss, missing executable, access
  denied and readiness timeout. Completed requires at least one measured CSV frame.
