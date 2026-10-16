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
  configuration, duration, tool hash/version and final result.
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
