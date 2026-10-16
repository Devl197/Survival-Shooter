using UnityEngine.AI;
using System;
using System.Collections.Generic;
using UnityEngine;


[DefaultExecutionOrder(-900)]
public class BenchmarkRunner : MonoBehaviour
{
    [Tooltip("Enable before Play Mode; select Record or Playback on SessionReplay.")]
    public bool runBenchmark;
    public BenchmarkScenario scenario;
    public EnemyAllocation allocation;
    public EnemyFactory factory;
    public EnemySpawner spawner;
    public SessionReplay replay;
    [SerializeField] string status = "Idle";
    [SerializeField] int activeCount;
    [SerializeField] int spawned;
    [SerializeField] int removed;
    [SerializeField] int poolExpansions;
    [SerializeField] float preparationMilliseconds;
    public string Status { get { return status; } }
    public bool Selected { get { return selected; } }
    readonly Queue<BenchmarkReplacement> dying = new Queue<BenchmarkReplacement>();
    float deathDuration;

BenchmarkSchedule schedule;
    EnemyHealth[] active;

    Vector3[] positions;
    Quaternion[] rotations;

    PlayerHealth player;
    bool originalInvulnerability;
    bool ownsPlayer;
    bool running;
    bool selected;
    float duration;
    EnemyAllocation selectedAllocation;
    int nextEvent;

    void Awake()
    {
        selected = runBenchmark;
        if (!selected) return;
        if (replay == null) replay = FindObjectOfType<SessionReplay>();
        if (factory == null) factory = FindObjectOfType<EnemyFactory>();
        if (spawner == null) spawner = FindObjectOfType<EnemySpawner>();
        if (replay != null) replay.ManagedByBenchmark = true;
        foreach (WaveManager manager in FindObjectsOfType<WaveManager>()) manager.enabled = false;
        foreach (EnemyManager manager in FindObjectsOfType<EnemyManager>())
        {
            manager.StopSpawning();
            manager.enabled = false;
        }
        foreach (PowerUpManager manager in FindObjectsOfType<PowerUpManager>()) manager.DisableForBenchmark();
        foreach (PowerUp pickup in FindObjectsOfType<PowerUp>()) pickup.gameObject.SetActive(false);
        foreach (EnemyHealth enemy in FindObjectsOfType<EnemyHealth>()) enemy.gameObject.SetActive(false);
    }

    void Start()
    {
        if (!selected) return;
        var preparation = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            if (scenario == null || spawner == null || factory == null || replay == null || !replay.isActiveAndEnabled ||
                replay.startupMode == ReplayStartupMode.Normal || factory.enemyPrefab == null ||
                factory.spawnPoints == null || scenario.typeWeights == null ||
                scenario.typeWeights.Length != factory.enemyPrefab.Length ||
                !Enum.IsDefined(typeof(EnemyAllocation), allocation))
                throw new InvalidOperationException("Assign scenario, factory and an enabled Record/Playback replay.");
            selectedAllocation = allocation;
            duration = replay.PrepareBenchmark();
            deathDuration = scenario.deathDuration;
            if (float.IsNaN(deathDuration) || float.IsInfinity(deathDuration) || deathDuration <= 0f)
                throw new InvalidOperationException("Death duration must be positive.");
            schedule = BenchmarkSchedule.Build(scenario.enemyCount, scenario.groupSize,
                scenario.replacementInterval, scenario.initialLifetime, duration, scenario.seed,
                scenario.typeWeights, factory.spawnPoints.Length, scenario.pattern);
            schedule.replacements.RemoveAll(change => change.time + deathDuration >= duration);
            var lastDeath = new float[scenario.enemyCount];
            for (int i = 0; i < lastDeath.Length; i++) lastDeath[i] = float.NegativeInfinity;
            foreach (BenchmarkReplacement change in schedule.replacements)
            {
                if (change.time - lastDeath[change.slot] <= deathDuration)
                    throw new InvalidOperationException("Death duration must be shorter than the replacement cycle of each slot.");
                lastDeath[change.slot] = change.time;
            }
            positions = new Vector3[factory.spawnPoints.Length];
            rotations = new Quaternion[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                if (factory.spawnPoints[i] == null) throw new InvalidOperationException("Missing spawn point.");
                positions[i] = factory.spawnPoints[i].position;
                rotations[i] = factory.spawnPoints[i].rotation;
            }
            for (int i = 0; i < factory.enemyPrefab.Length; i++)
            {
                GameObject prefab = factory.enemyPrefab[i];
                if (prefab == null || !prefab.activeSelf || prefab.GetComponent<EnemyHealth>() == null ||
                    prefab.GetComponent<EnemyAttack>() == null || prefab.GetComponent<Rigidbody>() == null ||
                    prefab.GetComponent<NavMeshAgent>() == null)
                    throw new InvalidOperationException("Enemy prefabs require health, attack, Rigidbody and NavMeshAgent.");
            }
            player = FindObjectOfType<PlayerHealth>();
            if (player == null) throw new InvalidOperationException("Missing player health.");
            originalInvulnerability = player.BenchmarkInvulnerable;
            player.BenchmarkInvulnerable = true;
            ownsPlayer = true;
            spawner.Initialize(selectedAllocation);
            active = new EnemyHealth[scenario.enemyCount];
            var capacities = new int[factory.enemyPrefab.Length];
            foreach (int type in schedule.types) capacities[type]++;
            for (int type = 0; type < capacities.Length; type++)
                spawner.Prewarm(type, capacities[type], positions[0], rotations[0]);
            for (int slot = 0; slot < active.Length; slot++) Spawn(slot, schedule.initialPoints[slot]);
            preparation.Stop();
            preparationMilliseconds = (float)preparation.Elapsed.TotalMilliseconds;
            if (!replay.BeginBenchmark()) throw new InvalidOperationException("Replay could not start; check its file and duration.");
            status = "Running";
            running = true;
            Debug.Log("Benchmark started: " + scenario.name + ", " + selectedAllocation +
                ", population=" + active.Length + ", replacements=" + schedule.replacements.Count +
                ", preparation=" + preparationMilliseconds + " ms", this);
        }
        catch (Exception exception)
        {
            status = "Failed";
            Cleanup();
            Debug.LogError("Benchmark failed: " + exception.Message, this);
        }
    }

    void Update()
    {
        if (!running || Time.timeScale == 0f) return;
        if (!replay.IsPlaying && !replay.IsRecording) { Finish("Stopped"); return; }
        float elapsed = Mathf.Min(Time.time - replay.StartedAt, duration);
        try
        {
            while (true)
            {
                float nextDeath = nextEvent < schedule.replacements.Count ? schedule.replacements[nextEvent].time : float.PositiveInfinity;
                float nextRemoval = dying.Count > 0 ? dying.Peek().time + deathDuration : float.PositiveInfinity;
                if (Mathf.Min(nextDeath, nextRemoval) > elapsed) break;
                if (nextRemoval <= nextDeath)
                {
                    BenchmarkReplacement change = dying.Dequeue();
                    Release(change.slot);
                    Spawn(change.slot, change.spawnPoint);
                }
                else
                {
                    BenchmarkReplacement change = schedule.replacements[nextEvent++];
                    active[change.slot].BeginBenchmarkDeath();
                    dying.Enqueue(change);
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogError("Benchmark execution failed: " + exception.Message, this);
            Finish("Failed");
        }
    }

    void LateUpdate()
    {
        if (!running) return;
        if (Time.time - replay.StartedAt >= duration) Finish("Completed");
        else if (!replay.IsRecording && !replay.IsPlaying) Finish("Stopped");
    }

    void Spawn(int slot, int point)
    {
        active[slot] = spawner.Spawn(schedule.types[slot], positions[point], rotations[point], true);
        poolExpansions = spawner.PoolExpansions;
        spawned++;
        activeCount++;
    }

    void Release(int slot)
    {
        if (active[slot] == null) return;
        spawner.Despawn(active[slot]);
        active[slot] = null;
        removed++;
        activeCount--;
    }

    void Finish(string result)
    {
        running = false;
        status = result;
        replay.Stop();
        Debug.Log("Benchmark " + result + ": spawned=" + spawned + ", removed=" + removed +
            ", active=" + activeCount + ", pool expansions=" + poolExpansions +
            ". Final cleanup is excluded from these counters.", this);
        Cleanup();
    }

    void Cleanup()
    {
        if (spawner != null) spawner.Clear();
        activeCount = 0;
        active = null;

        schedule = null;
        dying.Clear();
        if (ownsPlayer && player != null) player.BenchmarkInvulnerable = originalInvulnerability;
        ownsPlayer = false;
    }

    void OnDisable()
    {
        if (running) Finish("Stopped");
    }
}
