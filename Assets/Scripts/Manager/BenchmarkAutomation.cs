using System;
using System.IO;
using UnityEngine;

// One process executes one request. The external series runner owns repetition and timeouts.
public static class BenchmarkAutomation
{
    [Serializable]
    public sealed class GraphicsSettings
    {
        public int width, height;
        public bool fullscreen;
        public string quality;
    }

    [Serializable]
    public sealed class Request
    {
        public string scene, scenario, allocation, replayPath, presentMonPath, resultsDirectory, reportPath;
        public string animatorCulling = "CullUpdateTransforms";
        public string mainLightShadows = "Scene";
        public bool periodicPathUpdates, updateWhenOffscreen;
        public bool skinnedMotionVectors = true;
        public bool captureWithPresentMon = true;
        public float pathUpdateIntervalSeconds = 0.2f;
        public GraphicsSettings graphics;
    }

    [Serializable]
    sealed class Report
    {
        public string result, error, captureDirectory;
    }

    public static bool Active { get; private set; }
    public static string MainLightShadows { get { return Active && request != null ? request.mainLightShadows : "Scene"; } }
    static bool completed;
    static Request request;

    public static bool TryRead(out Request value)
    {
        value = null;
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--benchmark-request");
        if (index < 0) return false;
        Active = true;
        if (Application.isEditor) throw new InvalidOperationException("Automation requires a standalone Windows build.");
        if (index + 1 >= args.Length) throw new ArgumentException("Missing benchmark request path.");
        request = JsonUtility.FromJson<Request>(File.ReadAllText(args[index + 1]));
        if (request == null || !Path.IsPathRooted(request.reportPath ?? ""))
            throw new InvalidDataException("Request must specify an absolute reportPath.");
        value = request;
        return true;
    }

    public static void ApplyGraphics()
    {
        // Resolution/fullscreen are passed to the native player before scene startup.
        // Old request files without graphics keep their existing behavior.
        if (!Active || request.graphics == null) return;
        var graphics = request.graphics;
        if (graphics.width < 320 || graphics.width > 16384 || graphics.height < 200 || graphics.height > 16384)
            throw new InvalidDataException("Invalid requested resolution.");
        int quality = Array.IndexOf(QualitySettings.names, graphics.quality);
        if (quality < 0) throw new InvalidDataException("Unknown quality level: " + graphics.quality);
        QualitySettings.SetQualityLevel(quality, true);
    }

    public static bool GraphicsMatch()
    {
        if (!Active || request.graphics == null) return true;
        var graphics = request.graphics;
        return Screen.width == graphics.width && Screen.height == graphics.height &&
            Screen.fullScreen == graphics.fullscreen &&
            QualitySettings.names[QualitySettings.GetQualityLevel()] == graphics.quality;
    }

    public static void Complete(string result, string error, string captureDirectory)
    {
        if (!Active || completed) return;
        completed = true;
        try
        {
            if (request != null && !string.IsNullOrEmpty(request.reportPath))
                File.WriteAllText(request.reportPath, JsonUtility.ToJson(new Report {
                    result = result, error = error, captureDirectory = captureDirectory
                }, true));
        }
        catch (Exception e) { Debug.LogError("Cannot write automation report: " + e.Message); }
        Application.Quit(result == "Completed" ? 0 : 1);
    }
}
