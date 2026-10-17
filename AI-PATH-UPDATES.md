# Enemy path updates

Select `Assets/Scripts/ScriptableObjects/EnemyPathUpdates.asset` in Unity.
All three gameplay enemy prefabs share this asset.

- **Periodic Updates off**: call `SetDestination` every frame, as before.
- **Periodic Updates on**: call it every **Update Interval** seconds (default 0.2).
- Change either field during Play Mode to apply it to all active and future enemies.
  Asset edits in Play Mode can persist after stopping; restore the desired defaults.
- In the starting test menu, toggle **Periodic enemy path updates** before starting
  either normal gameplay or a benchmark. The initial selection and displayed
  interval come from this asset; one build supports both modes. Edit **Path update
  interval (seconds)** in the menu to test different intervals with the same build.
  Decimal points and commas are accepted (for example `0.2` or `0,2`); values must
  be finite and at least `0.01`. Invalid input disables both start buttons. The
  interval is applied alongside the toggle when launching either mode and is
  recorded in the existing `pathUpdateIntervalSeconds` benchmark metadata field.

The first destination request after each spawn/pool activation is immediate.
Enemies keep moving along their existing path between requests. Death is still
checked every frame. Timing uses game time, so it follows pause/time scale.
Intervals below 0.01 are clamped; NaN/infinity fall back to 0.2 seconds.
Missing settings preserve per-frame updates. Unity's NavMeshAgent automatic
repath behavior is unchanged; this setting only throttles explicit destination requests.

This is a gameplay setting with no dependency on BenchmarkRunner, scenarios,
replay, or allocation mode. It applies equally in ordinary gameplay and benchmarks.
PresentMon `session.json` reports include `periodicPathUpdates` (boolean) and
`pathUpdateIntervalSeconds` (configured, validated interval in seconds, even when
periodic updates are disabled). These describe the spawned enemies at capture
preparation, including launches directly from the gameplay scene. Keep settings
unchanged throughout a measured run. Older reports lack these fields and should
be treated as unknown, not automatically as disabled.

## Verification in Unity

1. Run the game with the switch off; confirm normal pursuit and death behavior.
2. Turn it on and use 1 second temporarily to make delayed tracking visible.
3. Change to 0.2 seconds, then switch off while moving; pursuit should update
   every frame again immediately.
4. Exercise pool reuse and InstantiateDestroy; each spawn must start pursuing
   immediately, without inheriting the previous lifetime's timer.
5. Compare CPU/Navigation profiler timings and frame times with the same enemy
   count and player movement, using separate runs for each mode. Reduced request
   frequency alone is not proof of a frame-time improvement. Enemies spawned
   together can refresh together; this implementation does not stagger requests.
