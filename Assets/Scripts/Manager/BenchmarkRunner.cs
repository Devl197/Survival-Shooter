using UnityEngine.AI;
using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using UnityEngine;


[DefaultExecutionOrder(-900)]
public class BenchmarkRunner : MonoBehaviour
{
    [Tooltip("Enable before Play Mode; select Record or Playback on SessionReplay.")]
    public bool runBenchmark;
    public bool captureWithPresentMon;
    public bool overrideEnemyCulling;
    public AnimatorCullingMode animatorCulling = AnimatorCullingMode.CullUpdateTransforms;
    public bool updateWhenOffscreen;
    public bool skinnedMotionVectors = true;
    [Tooltip("Absolute path, or relative to the project/build directory.")]
    public string presentMonPath = "Tools/PresentMon/PresentMon-2.6.0-x64.exe";
    public float captureReadyTimeout = 30f;
    public float stabilizationSeconds = 1f;
    public float captureTailSeconds = 2f;
    PresentMonCapture capture;
    BenchmarkProfilerCapture profilerCapture;
    bool preparingCapture;
    bool finalizingCapture;

    float originalTimeScale;
    int originalVSyncCount;
    int originalTargetFrameRate;
    bool ownsFrameSettings;
    InputHandler captureInput;
    PlayerMovement captureMovement;
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
        if (BenchmarkLaunch.Applies)
        {
            runBenchmark = !BenchmarkLaunch.NormalGame;
            captureWithPresentMon = runBenchmark && BenchmarkLaunch.Capture;
            if (runBenchmark)
            {
                scenario = BenchmarkLaunch.Scenario;
                allocation = BenchmarkLaunch.Allocation;
                presentMonPath = BenchmarkLaunch.PresentMonPath;
                overrideEnemyCulling = true;
                animatorCulling = BenchmarkLaunch.AnimatorCulling;
                updateWhenOffscreen = BenchmarkLaunch.UpdateWhenOffscreen;
                skinnedMotionVectors = BenchmarkLaunch.SkinnedMotionVectors;
            }
            // SessionReplay (-1000) has already consumed its selection before this Awake (-900).
            BenchmarkLaunch.Clear();
        }
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
            originalVSyncCount = QualitySettings.vSyncCount;
            originalTargetFrameRate = Application.targetFrameRate;
            ownsFrameSettings = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Debug.Log("Benchmark frame settings: scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name +
                ", quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] +
                ", vSyncCount=" + QualitySettings.vSyncCount +
                ", targetFrameRate=" + Application.targetFrameRate +
                ", editor=" + Application.isEditor +
                ". Frame limits are restored when the benchmark ends.", this);
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
            if (overrideEnemyCulling)
                spawner.ConfigureBenchmarkCulling(animatorCulling, updateWhenOffscreen, skinnedMotionVectors);
            spawner.Initialize(selectedAllocation);
            active = new EnemyHealth[scenario.enemyCount];
            var capacities = new int[factory.enemyPrefab.Length];
            foreach (int type in schedule.types) capacities[type]++;
            for (int type = 0; type < capacities.Length; type++)
                spawner.Prewarm(type, capacities[type], positions[0], rotations[0]);
            for (int slot = 0; slot < active.Length; slot++) Spawn(slot, schedule.initialPoints[slot]);
            preparation.Stop();
            preparationMilliseconds = (float)preparation.Elapsed.TotalMilliseconds;
            profilerCapture = BenchmarkProfilerCapture.StartIfRequested();
            if (captureWithPresentMon)
            {
                if (Application.platform != RuntimePlatform.WindowsPlayer)
                    throw new InvalidOperationException("PresentMon capture requires a Windows standalone build, not the Unity Editor.");
                if (Time.timeScale != 1f || captureReadyTimeout <= 0 || stabilizationSeconds < 0 || captureTailSeconds < 0)
                    throw new InvalidOperationException("Capture requires timeScale 1 and valid capture timings.");
                StartCoroutine(PrepareCapture());
                return;
            }
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
        if (running && capture != null && (Time.timeScale != 1f || !Application.isFocused || capture.Exited))
        {

            Finish("Invalid: pause, focus loss or capture exit");
            return;
        }
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
                    using (new EnemyProfilerSample("Benchmark.ReplaceEnemy"))
                    {
                        BenchmarkReplacement change = dying.Dequeue();
                        Release(change.slot);
                        Spawn(change.slot, change.spawnPoint);
                    }
                }
                else
                {
                    using (new EnemyProfilerSample("Benchmark.BeginDeath"))
                    {
                        BenchmarkReplacement change = schedule.replacements[nextEvent++];
                        active[change.slot].BeginBenchmarkDeath();
                        dying.Enqueue(change);
                    }
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
        if (finalizingCapture) return;
        running = false;
        if (capture != null)
        {
            capture.MarkEnd(result);
            finalizingCapture = true;
            status = "Finalizing capture";
            StartCoroutine(FinalizeCapture());
            return;
        }
        status = result;
        replay.Stop();
        Debug.Log("Benchmark " + result + ": spawned=" + spawned + ", removed=" + removed +
            ", active=" + activeCount + ", pool expansions=" + poolExpansions +
            ". Final cleanup is excluded from these counters.", this);
        Cleanup();
    }

    void Cleanup()
    {
        if (profilerCapture != null)
        {
            profilerCapture.Dispose();
            profilerCapture = null;
        }
        RestoreFrameSettings();
        if (spawner != null) spawner.Clear();
        activeCount = 0;
        active = null;

        schedule = null;
        dying.Clear();
        if (ownsPlayer && player != null) player.BenchmarkInvulnerable = originalInvulnerability;
        ownsPlayer = false;
    }

    void RestoreFrameSettings()
    {
        if (!ownsFrameSettings) return;
        QualitySettings.vSyncCount = originalVSyncCount;
        Application.targetFrameRate = originalTargetFrameRate;
        ownsFrameSettings = false;
    }

    IEnumerator PrepareCapture()
    {
        preparingCapture = true;
        status = "Waiting for PresentMon";
        originalTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        captureInput = FindObjectOfType<InputHandler>();
        captureMovement = FindObjectOfType<PlayerMovement>();
        if (captureInput != null) captureInput.ReplayControlsInput = true;
        if (captureMovement != null) captureMovement.ReplayControlsInput = true;
        bool started = false;
        try
        {
            capture = new PresentMonCapture();
            capture.Run.scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            RecordPathUpdateSettings(capture.Run);
            capture.Run.enemyCullingOverridden = overrideEnemyCulling;
            capture.Run.animatorCullingMode = overrideEnemyCulling ? animatorCulling.ToString() : "Prefab settings";
            capture.Run.updateWhenOffscreen = updateWhenOffscreen;
            capture.Run.skinnedMotionVectors = skinnedMotionVectors;
            capture.Run.scenario = scenario.name;
            capture.Run.scenarioConfiguration = JsonUtility.ToJson(scenario);
            capture.Run.allocation = selectedAllocation.ToString();
            capture.Run.replayFile = replay.FilePath;
            capture.Run.seed = scenario.seed;
            capture.Run.enemyCount = scenario.enemyCount;
            capture.Run.duration = duration;
            capture.Run.vSyncCount = QualitySettings.vSyncCount;
            capture.Run.targetFrameRate = Application.targetFrameRate;
            string root = Path.GetDirectoryName(Application.dataPath);
            string executable = Path.IsPathRooted(presentMonPath) ? presentMonPath : Path.Combine(root, presentMonPath);
            capture.Start(executable, Path.Combine(Application.persistentDataPath, "Benchmarks"));
            started = true;
        }
        catch (Exception e)
        {
            if (capture != null) { capture.Run.result = "CaptureFailed"; capture.Run.error = e.Message; }
            Debug.LogError("PresentMon startup failed: " + e.Message, this);
        }
        float deadline = Time.realtimeSinceStartup + captureReadyTimeout;
        while (started && !capture.Ready && !capture.Exited && Time.realtimeSinceStartup < deadline)
        {
            if (Input.GetKeyDown(replay.stopKey)) { started = false; break; }
            yield return null;
        }
        bool ready = started && capture.Ready && !capture.Exited;
        if (ready)
        {
            float until = Time.realtimeSinceStartup + stabilizationSeconds;
            while (Time.realtimeSinceStartup < until)
            {
                if (Input.GetKeyDown(replay.stopKey) || capture.Exited) { ready = false; break; }
                yield return null;
            }
        }
        // Leave the preparation frame before starting the measured session.
        yield return null;
        ready = ready && Time.timeScale == 0f && !capture.Exited;
        RestorePreparation();
        if (!ready || !Application.isFocused)
        {
            status = "Failed";
            Debug.LogError("PresentMon did not become ready. Check capture.log and ETW permissions.", this);
            if (capture != null)
            {
                capture.Run.result = "CaptureFailed";
                if (string.IsNullOrEmpty(capture.Run.error)) capture.Run.error = "Readiness cancelled, timed out or lost focus. " + capture.Error;
                capture.Dispose(); capture = null;
            }
            Cleanup();
            yield break;
        }
        replay.Stopping += ReplayStopping;
        if (!replay.BeginBenchmark()) { Finish("Failed"); yield break; }
        capture.MarkStart();
        status = "Running";
        running = true;
    }

    void RecordPathUpdateSettings(PresentMonRun run)
    {
        // Read the actual spawned enemies, including runs started directly in the gameplay scene.
        // A single pair of metadata fields cannot describe mixed AI settings accurately.
        for (int i = 0; i < active.Length; i++)
        {
            var movement = active[i].GetComponent<EnemyMovement>();
            if (movement == null) throw new InvalidOperationException("Benchmark enemy is missing EnemyMovement.");
            bool periodic = movement.PeriodicPathUpdates;
            float interval = movement.PathUpdateInterval;
            if (i == 0)
            {
                run.periodicPathUpdates = periodic;
                run.pathUpdateIntervalSeconds = interval;
            }
            else if (run.periodicPathUpdates != periodic || run.pathUpdateIntervalSeconds != interval)
                throw new InvalidOperationException("Benchmark enemies must share the same path update settings.");
        }
    }

    void ReplayStopping()
    {
        if (capture == null || finalizingCapture) return;
        capture.MarkEnd("Stopped");
    }

    IEnumerator FinalizeCapture()
    {
        // End marker precedes Stop/save/cleanup. Tail permits in-flight GPU/display events to arrive.
        yield return null;
        replay.Stopping -= ReplayStopping;
        replay.Stop();
        float until = Time.realtimeSinceStartup + captureTailSeconds;
        while (Time.realtimeSinceStartup < until) yield return null;
        capture.RequestStop();
        float deadline = Time.realtimeSinceStartup + 10f;
        while (!capture.Exited && Time.realtimeSinceStartup < deadline) yield return null;
        try
        {
            if (!capture.Exited) throw new TimeoutException("PresentMon termination timed out.");
            capture.Complete();
            status = capture.Run.result;
            Debug.Log("PresentMon " + status + ": " + capture.DirectoryPath, this);
        }
        catch (Exception e) { status = "Failed"; Debug.LogError(e.Message, this); }
        finally { capture.Dispose(); capture = null; finalizingCapture = false; Cleanup(); }
    }

    void RestorePreparation()
    {
        if (!preparingCapture) return;
        preparingCapture = false;
        Time.timeScale = originalTimeScale;
        if (captureInput != null) captureInput.ReplayControlsInput = false;
        if (captureMovement != null) captureMovement.ReplayControlsInput = false;
    }

    void OnDisable()
    {
        StopAllCoroutines();
        RestorePreparation();
        if (capture != null)
        {
            capture.Run.result = "Aborted";
            capture.MarkEnd("Aborted");
            replay.Stopping -= ReplayStopping;
            capture.Dispose(); capture = null;
            running = finalizingCapture = false;
            Cleanup();
        }
        else if (running) Finish("Stopped");
        RestoreFrameSettings();
    }
}
