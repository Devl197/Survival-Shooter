using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Applied by the gameplay components before their existing Awake initialization.
public static class BenchmarkLaunch
{
    public static BenchmarkScenario Scenario;
    public static EnemyAllocation Allocation;
    public static string ReplayPath, Scene, PresentMonPath, ResultsDirectory;
    public static bool Capture, NormalGame;
    public static AnimatorCullingMode AnimatorCulling;
    public static bool UpdateWhenOffscreen, SkinnedMotionVectors;
    public static bool Applies { get { return !string.IsNullOrEmpty(Scene) && SceneManager.GetActiveScene().name == Scene; } }
    public static void Clear() { Scenario = null; ReplayPath = Scene = PresentMonPath = ResultsDirectory = null; Capture = NormalGame = false;
        AnimatorCulling = AnimatorCullingMode.CullUpdateTransforms; UpdateWhenOffscreen = false; SkinnedMotionVectors = true; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Reset() { Clear(); }
}

public class BenchmarkMenu : MonoBehaviour
{
    public EnemyPathUpdateSettings pathUpdateSettings;
    bool periodicPathUpdates;
    string pathUpdateInterval = "0.2";
    public BenchmarkScenario[] scenarios;
    public string gameScene = "Level_01";
    public string experimentalScene = "Level_01_VisualExperiment";
    bool useExperimentalScene;
    string SelectedScene { get { return useExperimentalScene ? experimentalScene : gameScene; } }
    string folder, replayPath = "", error = "";
    string resultsFolder = "";
    static string ResultsRoot { get { return Path.Combine(Application.persistentDataPath, "Benchmarks"); } }
    string presentMonPath = "Tools/PresentMon/PresentMon-2.6.0-x64.exe";
    string[] files = new string[0];
    int scenarioIndex, allocationIndex;
    bool capture = true, loading;
    int animatorCullingIndex = 1;
    bool updateWhenOffscreen, skinnedMotionVectors = true;
    static readonly string[] CullingNames = { "Always Animate", "Cull Update Transforms", "Cull Completely" };
    static readonly AnimatorCullingMode[] CullingModes = { AnimatorCullingMode.AlwaysAnimate,
        AnimatorCullingMode.CullUpdateTransforms, AnimatorCullingMode.CullCompletely };
    Vector2 scroll;
    string lastBenchmark = "";
    static string PreferencesKey { get { return "BenchmarkMenu.v1." + (Application.isEditor ? "Editor" : "Player"); } }

    [Serializable]
    sealed class SavedSettings
    {
        public string scenario, folder, replayPath, presentMonPath, pathUpdateInterval, resultsFolder;
        public int allocation, culling;
        public string scene;
        public bool capture, periodicPathUpdates, updateWhenOffscreen, skinnedMotionVectors;
    }

    void LoadSettings()
    {
        lastBenchmark = PlayerPrefs.GetString(PreferencesKey + ".LastBenchmark", "");
        if (!PlayerPrefs.HasKey(PreferencesKey)) return;
        try
        {
            var saved = JsonUtility.FromJson<SavedSettings>(PlayerPrefs.GetString(PreferencesKey));
            if (saved == null) return;
            useExperimentalScene = saved.scene == experimentalScene;
            if (scenarios != null)
            {
                int index = Array.FindIndex(scenarios, s => s != null && s.name == saved.scenario);
                if (index >= 0) scenarioIndex = index;
            }
            allocationIndex = Mathf.Clamp(saved.allocation, 0, 1);
            animatorCullingIndex = Mathf.Clamp(saved.culling, 0, CullingModes.Length - 1);
            folder = saved.folder ?? folder;
            resultsFolder = saved.resultsFolder ?? "";
            replayPath = saved.replayPath ?? replayPath;
            presentMonPath = saved.presentMonPath ?? presentMonPath;
            pathUpdateInterval = saved.pathUpdateInterval ?? pathUpdateInterval;
            capture = saved.capture;
            periodicPathUpdates = saved.periodicPathUpdates;
            updateWhenOffscreen = saved.updateWhenOffscreen;
            skinnedMotionVectors = saved.skinnedMotionVectors;
        }
        catch (Exception e) { Debug.LogWarning("Cannot restore benchmark menu settings: " + e.Message); }
    }

    void SaveSettings()
    {
        if (BenchmarkAutomation.Active) return;
        var saved = new SavedSettings {
            scene = SelectedScene,
            scenario = scenarios != null && scenarioIndex >= 0 && scenarioIndex < scenarios.Length && scenarios[scenarioIndex] != null
                ? scenarios[scenarioIndex].name : "",
            folder = folder, replayPath = replayPath, presentMonPath = presentMonPath, resultsFolder = resultsFolder,
            pathUpdateInterval = pathUpdateInterval, allocation = allocationIndex, culling = animatorCullingIndex,
            capture = capture, periodicPathUpdates = periodicPathUpdates,
            updateWhenOffscreen = updateWhenOffscreen, skinnedMotionVectors = skinnedMotionVectors
        };
        PlayerPrefs.SetString(PreferencesKey, JsonUtility.ToJson(saved));
        PlayerPrefs.Save();
    }

    string DescribeBenchmark(string path)
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " (local)\n" +
            "Scene: " + SelectedScene + " | " + scenarios[scenarioIndex].name + " | " + (EnemyAllocation)allocationIndex + "\n" +
            "Animator: " + CullingNames[animatorCullingIndex] +
            " | Offscreen: " + (updateWhenOffscreen ? "On" : "Off") +
            " | Motion vectors: " + (skinnedMotionVectors ? "On" : "Off") + "\n" +
            "Periodic AI: " + (periodicPathUpdates ? "On" : "Off") + " | Interval: " + pathUpdateInterval + " s\n" +
            "PresentMon: " + (capture ? "On" : "Off") + " | Executable: " + presentMonPath + "\n" +
            "Replay: " + path + "\nResults: " + GetResultsDirectory();
    }

    void OnDisable() { SaveSettings(); }
    void OnApplicationQuit() { SaveSettings(); }

    void Awake()
    {
        BenchmarkLaunch.Clear();
        periodicPathUpdates = pathUpdateSettings != null && pathUpdateSettings.periodicUpdates;
        if (pathUpdateSettings != null)
            pathUpdateInterval = pathUpdateSettings.Interval.ToString("R", CultureInfo.InvariantCulture);
        folder = Path.Combine(Application.persistentDataPath, "Replays");
        try
        {
            BenchmarkAutomation.Request request;
            if (BenchmarkAutomation.TryRead(out request))
            {
                ApplyAutomation(request);
                BenchmarkAutomation.ApplyGraphics();
                Launch();
                return;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Benchmark automation: " + e.Message);
            BenchmarkAutomation.Complete("Failed", e.Message, null);
            return;
        }
        LoadSettings();
        RefreshFiles();
        var cameraObject = new GameObject("Menu Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.06f, .09f, .13f);
        camera.cullingMask = 0;
    }

    void ApplyAutomation(BenchmarkAutomation.Request request)
    {
        if (request.scene != gameScene && request.scene != experimentalScene)
            throw new InvalidDataException("Unknown benchmark scene: " + request.scene);
        useExperimentalScene = request.scene == experimentalScene;
        scenarioIndex = scenarios == null ? -1 : Array.FindIndex(scenarios, s => s != null && s.name == request.scenario);
        if (scenarioIndex < 0) throw new InvalidDataException("Unknown benchmark scenario: " + request.scenario);
        if (request.allocation != "Pool" && request.allocation != "InstantiateDestroy")
            throw new InvalidDataException("Unknown allocation: " + request.allocation);
        allocationIndex = request.allocation == "Pool" ? 1 : 0;
        animatorCullingIndex = Array.FindIndex(CullingModes, mode => mode.ToString() == request.animatorCulling);
        if (animatorCullingIndex < 0) throw new InvalidDataException("Unknown animator culling mode.");
        if (!Path.IsPathRooted(request.resultsDirectory ?? ""))
            throw new InvalidDataException("Automation requires an absolute results directory.");
        automatedResultsDirectory = request.resultsDirectory;
        replayPath = request.replayPath;
        presentMonPath = request.presentMonPath;
        periodicPathUpdates = request.periodicPathUpdates;
        pathUpdateInterval = request.pathUpdateIntervalSeconds.ToString("R", CultureInfo.InvariantCulture);
        updateWhenOffscreen = request.updateWhenOffscreen;
        skinnedMotionVectors = request.skinnedMotionVectors;
        capture = request.captureWithPresentMon;
    }

    string automatedResultsDirectory;

    void RefreshFiles()
    {
        try
        {
            files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.json") : new string[0];
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(replayPath) && files.Length > 0) replayPath = files[0];
            error = files.Length == 0 ? "No replay files found. Enter a full file path or choose another folder." : "";
        }
        catch (Exception e) { files = new string[0]; error = e.Message; }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
    }

    void Launch()
    {
        try
        {
            string scene = SelectedScene;
            if (scenarios == null || scenarioIndex >= scenarios.Length || scenarios[scenarioIndex] == null)
                throw new InvalidOperationException("Select a benchmark scenario.");
            string path = Path.GetFullPath(replayPath.Trim());
            var session = JsonUtility.FromJson<ReplaySession>(File.ReadAllText(path));
            SessionReplay.ValidateForScene(session, scene);
            if (session.duration <= 0) throw new InvalidDataException("Replay duration must be positive.");
            if (!Application.CanStreamedLevelBeLoaded(scene))
                throw new InvalidOperationException("The gameplay scene is missing from Build Settings.");
            if (capture)
            {
                if (Application.platform != RuntimePlatform.WindowsPlayer)
                    throw new InvalidOperationException("PresentMon requires a Windows build. Disable capture to test the menu in the Editor.");
                string exe = Path.IsPathRooted(presentMonPath) ? presentMonPath :
                    Path.Combine(Path.GetDirectoryName(Application.dataPath), presentMonPath);
                if (!File.Exists(exe)) throw new FileNotFoundException("PresentMon executable not found. Enter its full path.");
            }
            string resultsDirectory = GetResultsDirectory();
            Directory.CreateDirectory(resultsDirectory);
            BenchmarkLaunch.ResultsDirectory = resultsDirectory;
            BenchmarkLaunch.Scenario = scenarios[scenarioIndex];
            BenchmarkLaunch.NormalGame = false;
            BenchmarkLaunch.Allocation = (EnemyAllocation)allocationIndex;
            BenchmarkLaunch.ReplayPath = path;
            BenchmarkLaunch.Scene = scene;
            BenchmarkLaunch.Capture = capture;
            BenchmarkLaunch.PresentMonPath = presentMonPath;
            BenchmarkLaunch.AnimatorCulling = CullingModes[animatorCullingIndex];
            BenchmarkLaunch.UpdateWhenOffscreen = updateWhenOffscreen;
            BenchmarkLaunch.SkinnedMotionVectors = skinnedMotionVectors;
            ApplyPathUpdateSettings();
            SaveSettings();
            string description = DescribeBenchmark(path);
            loading = true;
            SceneManager.LoadScene(scene);
            lastBenchmark = description;
            if (!BenchmarkAutomation.Active)
            {
                PlayerPrefs.SetString(PreferencesKey + ".LastBenchmark", lastBenchmark);
                PlayerPrefs.Save();
            }
        }
        catch (Exception e) { BenchmarkLaunch.Clear(); loading = false; error = e.Message;
            BenchmarkAutomation.Complete("Failed", e.Message, null); }
    }

    void LaunchNormalGame()
    {
        try
        {
            if (!Application.CanStreamedLevelBeLoaded(gameScene))
                throw new InvalidOperationException("The gameplay scene is missing from Build Settings.");
            BenchmarkLaunch.Clear();
            BenchmarkLaunch.NormalGame = true;
            BenchmarkLaunch.Scene = gameScene;
            ApplyPathUpdateSettings();
            loading = true;
            SceneManager.LoadScene(gameScene);
        }
        catch (Exception e) { BenchmarkLaunch.Clear(); loading = false; error = e.Message; }
    }

    string GetResultsDirectory()
    {
        if (BenchmarkAutomation.Active) return automatedResultsDirectory;
        string name = resultsFolder.Trim();
        if (name.Length == 0) return ResultsRoot;
        if (name == "." || name == ".." || name.EndsWith(".", StringComparison.Ordinal) ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.IndexOfAny("<>:\"/\\|?*".ToCharArray()) >= 0)
            throw new InvalidOperationException("Enter a single results subfolder name, without path separators or invalid characters.");
        string stem = name.Split('.')[0].TrimEnd().ToUpperInvariant();
        if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" ||
            (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
            "123456789\u00b9\u00b2\u00b3".IndexOf(stem[3]) >= 0))
            throw new InvalidOperationException("This results subfolder name is reserved by Windows. Choose another name.");
        return Path.Combine(ResultsRoot, name);
    }

    void ApplyPathUpdateSettings()
    {
        if (pathUpdateSettings == null)
            throw new InvalidOperationException("Assign the shared enemy path update settings asset.");
        float interval;
        if (!TryGetPathUpdateInterval(out interval))
            throw new InvalidOperationException("Path update interval must be a finite number of at least 0.01 seconds.");
        pathUpdateSettings.updateInterval = interval;
        pathUpdateSettings.periodicUpdates = periodicPathUpdates;
    }

    bool TryGetPathUpdateInterval(out float interval)
    {
        // Accept either decimal separator without interpreting commas as thousands.
        return float.TryParse(pathUpdateInterval.Replace(',', '.'), NumberStyles.Float,
            CultureInfo.InvariantCulture, out interval) &&
            !float.IsNaN(interval) && !float.IsInfinity(interval) && interval >= 0.01f;
    }

    void OnGUI()
    {
        if (BenchmarkAutomation.Active) return;
        float scale = Mathf.Max(.4f, Mathf.Min(Screen.width / 960f, Screen.height / 740f));
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
        float width = Screen.width / scale;
        GUILayout.BeginArea(new Rect((width - 880) / 2, 20, 880, Screen.height / scale - 40), GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("SURVIVAL SHOOTER", new GUIStyle(GUI.skin.label) { fontSize = 24 });
        GUI.enabled = !loading;
        periodicPathUpdates = GUILayout.Toggle(periodicPathUpdates, "Periodic enemy path updates (normal game and benchmark)");
        GUILayout.BeginHorizontal();
        GUILayout.Label("Path update interval (seconds)", GUILayout.Width(240));
        pathUpdateInterval = GUILayout.TextField(pathUpdateInterval, GUILayout.Width(160));
        GUILayout.EndHorizontal();
        GUILayout.Label("Minimum 0.01 s; use a decimal point or comma. Periodic updates off: update every frame.");
        float parsedInterval;
        bool validInterval = TryGetPathUpdateInterval(out parsedInterval);
        if (!validInterval)
            GUILayout.Label("Enter a finite interval of at least 0.01 seconds.",
                new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(1, .65f, .45f) } });
        GUILayout.Space(10);
        GUI.enabled = !loading && validInterval;
        if (GUILayout.Button("Play normal game", GUILayout.Height(44))) LaunchNormalGame();
        GUI.enabled = true;
        GUILayout.Label("Normal gameplay uses waves and power-ups. No replay or PresentMon is required.");
        GUILayout.Space(14);
        GUILayout.Label("BENCHMARK", new GUIStyle(GUI.skin.label) { fontSize = 20 });
        GUILayout.Label("Scene (benchmark only)");
        useExperimentalScene = GUILayout.SelectionGrid(useExperimentalScene ? 1 : 0,
            new[] { gameScene + " (original)", experimentalScene + " (visual experiments)" },
            2, GUILayout.Height(34)) == 1;
        GUILayout.Label("Choose settings, then start one measured playback. Restart the application for the next run.");
        if (!string.IsNullOrEmpty(lastBenchmark))
        {
            GUILayout.Label("LAST LAUNCHED BENCHMARK (completion not tracked)");
            GUILayout.Label(lastBenchmark, new GUIStyle(GUI.skin.label) { wordWrap = true });
        }
        GUILayout.Space(14);
        GUILayout.Label("Scenario");
        if (scenarios != null)
        {
            string[] names = Array.ConvertAll(scenarios, s => s == null ? "Missing scenario" : s.name);
            if (names.Length > 0) scenarioIndex = GUILayout.SelectionGrid(scenarioIndex, names, 2,
                GUILayout.Height(32 * Mathf.CeilToInt(names.Length / 2f)));
            if (scenarioIndex < scenarios.Length && scenarios[scenarioIndex] != null)
            {
                var s = scenarios[scenarioIndex];
                GUILayout.Label(string.Format("{0} enemies | {1} | group {2} / {3} s | death duration {4} s",
                    s.enemyCount, s.pattern, s.groupSize, s.replacementInterval, s.deathDuration));
            }
        }
        GUILayout.Space(10);
        GUILayout.Label("Enemy allocation");
        allocationIndex = GUILayout.SelectionGrid(allocationIndex, new[] { "Instantiate / Destroy", "Pool" }, 2, GUILayout.Height(34));
        GUILayout.Space(10);
        GUILayout.Label("Animator Culling Mode (benchmark only)");
        animatorCullingIndex = GUILayout.SelectionGrid(animatorCullingIndex, CullingNames, 3, GUILayout.Height(34));
        updateWhenOffscreen = GUILayout.Toggle(updateWhenOffscreen, "Update When Offscreen");
        skinnedMotionVectors = GUILayout.Toggle(skinnedMotionVectors, "Skinned Motion Vectors");
        GUILayout.Label("Applied to all enemy animators and skinned meshes for this benchmark.");
        GUILayout.Space(10);
        GUILayout.Label("Replay folder");
        GUILayout.BeginHorizontal();
        folder = GUILayout.TextField(folder);
        if (GUILayout.Button("Refresh", GUILayout.Width(100))) RefreshFiles();
        GUILayout.EndHorizontal();
        foreach (string file in files)
            if (GUILayout.Toggle(string.Equals(file, replayPath, StringComparison.OrdinalIgnoreCase), Path.GetFileName(file))) replayPath = file;
        GUILayout.Label("Selected replay (full path; editable)");
        replayPath = GUILayout.TextField(replayPath);
        GUILayout.Label("Duration comes from the replay. The selected file is only read, never overwritten.");
        GUILayout.Space(10);
        GUILayout.Label("Results subfolder (category; optional)");
        resultsFolder = GUILayout.TextField(resultsFolder);
        bool validResultsFolder = true;
        try
        {
            GUILayout.Label("Save to: " + GetResultsDirectory(), new GUIStyle(GUI.skin.label) { wordWrap = true });
        }
        catch (Exception e)
        {
            validResultsFolder = false;
            GUILayout.Label(e.Message, new GUIStyle(GUI.skin.label) { wordWrap = true, normal = { textColor = new Color(1, .65f, .45f) } });
        }
        GUILayout.Label("Created automatically when starting. Leave empty to use Benchmarks. Each capture keeps its own run folder.");
        GUILayout.Space(10);
        capture = GUILayout.Toggle(capture, "Capture with PresentMon (Windows build)");
        if (capture)
        {
            GUILayout.Label("PresentMon executable (absolute path, or relative to the game's folder)");
            presentMonPath = GUILayout.TextField(presentMonPath);
        }
        GUILayout.Space(12);
        if (!string.IsNullOrEmpty(error)) GUILayout.Label(error, new GUIStyle(GUI.skin.label) { wordWrap = true, normal = { textColor = new Color(1, .65f, .45f) } });
        GUI.enabled = !loading && validInterval && validResultsFolder;
        if (GUILayout.Button(loading ? "Loading..." : "Start benchmark", GUILayout.Height(44))) Launch();
        GUI.enabled = true;
        if (GUILayout.Button("Quit", GUILayout.Height(30))) Application.Quit();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        GUI.matrix = previous;
    }
}
