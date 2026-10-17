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
    public static string ReplayPath, Scene, PresentMonPath;
    public static bool Capture, NormalGame;
    public static bool Applies { get { return !string.IsNullOrEmpty(Scene) && SceneManager.GetActiveScene().name == Scene; } }
    public static void Clear() { Scenario = null; ReplayPath = Scene = PresentMonPath = null; Capture = NormalGame = false; }

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
    string folder, replayPath = "", error = "";
    string presentMonPath = "Tools/PresentMon/PresentMon-2.6.0-x64.exe";
    string[] files = new string[0];
    int scenarioIndex, allocationIndex;
    bool capture = true, loading;
    Vector2 scroll;

    void Awake()
    {
        BenchmarkLaunch.Clear();
        periodicPathUpdates = pathUpdateSettings != null && pathUpdateSettings.periodicUpdates;
        if (pathUpdateSettings != null)
            pathUpdateInterval = pathUpdateSettings.Interval.ToString("R", CultureInfo.InvariantCulture);
        folder = Path.Combine(Application.persistentDataPath, "Replays");
        RefreshFiles();
        var cameraObject = new GameObject("Menu Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.06f, .09f, .13f);
        camera.cullingMask = 0;
    }

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
            if (scenarios == null || scenarioIndex >= scenarios.Length || scenarios[scenarioIndex] == null)
                throw new InvalidOperationException("Select a benchmark scenario.");
            string path = Path.GetFullPath(replayPath.Trim());
            var session = JsonUtility.FromJson<ReplaySession>(File.ReadAllText(path));
            SessionReplay.ValidateForScene(session, gameScene);
            if (session.duration <= 0) throw new InvalidDataException("Replay duration must be positive.");
            if (!Application.CanStreamedLevelBeLoaded(gameScene))
                throw new InvalidOperationException("The gameplay scene is missing from Build Settings.");
            if (capture)
            {
                if (Application.platform != RuntimePlatform.WindowsPlayer)
                    throw new InvalidOperationException("PresentMon requires a Windows build. Disable capture to test the menu in the Editor.");
                string exe = Path.IsPathRooted(presentMonPath) ? presentMonPath :
                    Path.Combine(Path.GetDirectoryName(Application.dataPath), presentMonPath);
                if (!File.Exists(exe)) throw new FileNotFoundException("PresentMon executable not found. Enter its full path.");
            }
            BenchmarkLaunch.Scenario = scenarios[scenarioIndex];
            BenchmarkLaunch.NormalGame = false;
            BenchmarkLaunch.Allocation = (EnemyAllocation)allocationIndex;
            BenchmarkLaunch.ReplayPath = path;
            BenchmarkLaunch.Scene = gameScene;
            BenchmarkLaunch.Capture = capture;
            BenchmarkLaunch.PresentMonPath = presentMonPath;
            ApplyPathUpdateSettings();
            loading = true;
            SceneManager.LoadScene(gameScene);
        }
        catch (Exception e) { BenchmarkLaunch.Clear(); loading = false; error = e.Message; }
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
        GUILayout.Label("Choose settings, then start one measured playback. Restart the application for the next run.");
        GUILayout.Space(14);
        GUILayout.Label("Scenario");
        if (scenarios != null)
        {
            string[] names = Array.ConvertAll(scenarios, s => s == null ? "Missing scenario" : s.name);
            if (names.Length > 0) scenarioIndex = GUILayout.SelectionGrid(scenarioIndex, names, 2, GUILayout.Height(64));
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
        capture = GUILayout.Toggle(capture, "Capture with PresentMon (Windows build)");
        if (capture)
        {
            GUILayout.Label("PresentMon executable (absolute path, or relative to the game's folder)");
            presentMonPath = GUILayout.TextField(presentMonPath);
        }
        GUILayout.Space(12);
        if (!string.IsNullOrEmpty(error)) GUILayout.Label(error, new GUIStyle(GUI.skin.label) { wordWrap = true, normal = { textColor = new Color(1, .65f, .45f) } });
        GUI.enabled = !loading && validInterval;
        if (GUILayout.Button(loading ? "Loading..." : "Start benchmark", GUILayout.Height(44))) Launch();
        GUI.enabled = true;
        if (GUILayout.Button("Quit", GUILayout.Height(30))) Application.Quit();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        GUI.matrix = previous;
    }
}
