using System;
using System.Collections;
using System.IO;
using UnityEngine;

// Opt-in visual capture only; never enable this during performance measurements.
[DefaultExecutionOrder(10000)]
public sealed class ShadowScreenshotCapture : MonoBehaviour
{
    string directory;
    bool capturing;
    float elapsed;

    [Serializable]
    sealed class Metadata
    {
        public string scene, quality, light, scenario;
        public float requestedReplaySeconds = 1f, actualReplaySeconds;
        public int width, height, simulationFrameRate = 60;
        public bool developmentBuild, editor;
        public string method = "Same paused replay frame; Soft then Off; framebuffer PNG including HUD. Not a performance measurement.";
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--shadow-screenshots");
        if (index < 0) return;
        if (Application.isEditor || Debug.isDebugBuild || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
            throw new InvalidOperationException("Shadow screenshots require a Release player and an absolute output directory.");
        var host = new GameObject("Shadow screenshot capture");
        DontDestroyOnLoad(host);
        host.AddComponent<ShadowScreenshotCapture>().directory = args[index + 1];
        Time.captureFramerate = 60;
    }

    void LateUpdate()
    {
        if (capturing) return;
        var runner = FindObjectOfType<BenchmarkRunner>();
        if (runner == null || runner.Status != "Running" || runner.replay == null) return;
        elapsed = runner.replay.Elapsed;
        if (elapsed + 0.0001f < 1f) return;
        capturing = true;
        StartCoroutine(Capture(runner));
    }

    IEnumerator Capture(BenchmarkRunner runner)
    {
        var shadows = new BenchmarkShadowOverride();
        float originalTimeScale = Time.timeScale;
        try
        {
            Directory.CreateDirectory(directory);
            // Scene mode avoids conflicting with the benchmark's own shadow validation.
            if (BenchmarkAutomation.MainLightShadows != "Scene")
                throw new InvalidOperationException("Use mainLightShadows=Scene for paired screenshots.");
            Time.timeScale = 0f;
            shadows.Apply("Soft");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            Application.Quit(1);
            yield break;
        }

        // Render both variants after LateUpdate, without advancing gameplay between them.
        // Let the threaded renderer and replay HUD settle on the paused frame.
        for (int frame = 0; frame < 3; frame++)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
        }
        bool success = false;
        try
        {
            Save("senke-ukljucene.png");
            string lightName = shadows.LightName;
            shadows.Restore();
            shadows.Apply("Off");
            File.WriteAllText(Path.Combine(directory, "capture.json"), JsonUtility.ToJson(new Metadata {
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                quality = QualitySettings.names[QualitySettings.GetQualityLevel()], light = lightName,
                scenario = runner.scenario.name, actualReplaySeconds = elapsed,
                width = Screen.width, height = Screen.height,
                developmentBuild = Debug.isDebugBuild, editor = Application.isEditor
            }, true));
            success = true;
        }
        catch (Exception error) { Debug.LogException(error); }
        if (success)
        {
            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
                yield return new WaitForEndOfFrame();
            }
            try { Save("senke-iskljucene.png"); }
            catch (Exception error) { Debug.LogException(error); success = false; }
        }
        shadows.Restore();
        Time.timeScale = originalTimeScale;
        Time.captureFramerate = 0;
        Debug.Log("Shadow screenshot capture: " + (success ? "Completed " : "Failed ") + directory);
        Application.Quit(success ? 0 : 1);
    }

    void Save(string name)
    {
        Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
        try { File.WriteAllBytes(Path.Combine(directory, name), image.EncodeToPNG()); }
        finally { Destroy(image); }
    }
}
