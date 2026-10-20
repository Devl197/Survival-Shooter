using System;
using System.IO;
using UnityEngine;

// One process executes one request. The external series runner owns repetition and timeouts.
public static class BenchmarkAutomation
{
    [Serializable]
    public sealed class Request
    {
        public string scene, scenario, allocation, replayPath, presentMonPath, resultsDirectory, reportPath;
        public string animatorCulling = "CullUpdateTransforms";
        public bool periodicPathUpdates, updateWhenOffscreen;
        public bool skinnedMotionVectors = true;
        public float pathUpdateIntervalSeconds = 0.2f;
    }

    [Serializable]
    sealed class Report
    {
        public string result, error, captureDirectory;
    }

    public static bool Active { get; private set; }
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
