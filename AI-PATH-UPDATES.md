# Enemy path updates

Select `Assets/Scripts/ScriptableObjects/EnemyPathUpdates.asset` in Unity.
All three gameplay enemy prefabs share this asset.

- **Periodic Updates off** (default): call `SetDestination` every frame, as before.
- **Periodic Updates on**: call it every **Update Interval** seconds (default 0.2).
- Change either field during Play Mode to apply it to all active and future enemies.
  Asset edits in Play Mode can persist after stopping; restore the desired defaults.
- For standalone comparisons, set the asset before building each variant.

The first destination request after each spawn/pool activation is immediate.
Enemies keep moving along their existing path between requests. Death is still
checked every frame. Timing uses game time, so it follows pause/time scale.
Intervals below 0.01 are clamped; NaN/infinity fall back to 0.2 seconds.
Missing settings preserve per-frame updates. Unity's NavMeshAgent automatic
repath behavior is unchanged; this setting only throttles explicit destination requests.

This is a gameplay setting with no dependency on BenchmarkRunner, scenarios,
replay, or allocation mode. It applies equally in ordinary gameplay and benchmarks.
Benchmark reports do not record this setting; label comparison runs with the
mode and interval yourself.

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
