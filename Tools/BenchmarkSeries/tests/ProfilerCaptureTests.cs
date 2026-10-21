// Tests recording lifecycle only. These stubs never produce real Unity profiler data.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace UnityEngine
{
    public static class Time { public static int frameCount; public static float realtimeSinceStartup; }
    public static class Application { public static string unityVersion = "2018.3-test"; public static int targetFrameRate = 60; }
    public static class JsonUtility
    {
        public static string ToJson(object value, bool pretty) { return new JavaScriptSerializer().Serialize(value); }
    }
}
namespace UnityEngine.Profiling
{
    public static class Profiler
    {
        public static bool enabled;
        public static int maxUsedMemory = 16 * 1024 * 1024;
        public static string logFile = "original";
        static bool binaryLog;
        public static bool enableBinaryLog
        {
            get { return binaryLog; }
            set {
                if (binaryLog && !value) File.WriteAllText(logFile, "test-double");
                binaryLog = value;
            }
        }
    }
}
class ProfilerCaptureTests
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static int Main(string[] args)
    {
        if (Array.IndexOf(args, "--benchmark-profiler-log") < 0)
        {
            Check(BenchmarkProfilerCapture.StartIfRequested("BenchmarkBurst", "Pool") == null, "Capture must be opt-in.");
            return 0;
        }
#if !DEVELOPMENT_BUILD
        bool rejected = false;
        try { BenchmarkProfilerCapture.StartIfRequested("BenchmarkBurst", "Pool"); }
        catch (InvalidOperationException e) { rejected = e.Message.Contains("Development Build"); }
        Check(rejected, "Release build must reject requested profiling.");
#else
        string directory = args[Array.IndexOf(args, "--benchmark-profiler-directory") + 1];
        UnityEngine.Time.frameCount = 100;
        UnityEngine.Time.realtimeSinceStartup = 10;
        using (var capture = BenchmarkProfilerCapture.StartIfRequested("BenchmarkBurst", "Pool"))
        {
            Check(!UnityEngine.Profiling.Profiler.enabled, "Preparation must not be recorded.");
            capture.Begin();
            Check(UnityEngine.Profiling.Profiler.maxUsedMemory == 128 * 1024 * 1024, "Profiler buffer was not configured.");
            for (int frame = 101; frame <= 701; frame++)
            {
                UnityEngine.Time.frameCount = frame;
                UnityEngine.Time.realtimeSinceStartup = 10 + (frame - 100) / 60f;
                capture.Advance();
            }
            capture.Stop("Completed", 420, 120, 0);
            capture.Stop("Aborted", 0, 0, 0);
        }
        Check(!UnityEngine.Profiling.Profiler.enabled && !UnityEngine.Profiling.Profiler.enableBinaryLog &&
            UnityEngine.Profiling.Profiler.logFile == "original" && UnityEngine.Profiling.Profiler.maxUsedMemory == 16 * 1024 * 1024, "Profiler state was not restored.");
        var manifest = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(directory, "profiler.json")));
        Check((string)manifest["result"] == "Completed" && (int)manifest["removed"] == 120, "Completion must not be overwritten on disposal.");
        var segments = (ArrayList)manifest["segments"];
        Check(segments.Count == 4, "Expected 200 + 200 + 200 + 1 frames.");
        int expectedStart = 100;
        foreach (Dictionary<string, object> segment in segments)
        {
            int start = (int)segment["startFrame"], end = (int)segment["endFrame"];
            Check(start == expectedStart && end - start <= 200, "Segment coverage has a gap or exceeds 200 frames.");
            Check(File.Exists(Path.Combine(directory, (string)segment["file"])), "Segment was not flushed.");
            expectedStart = end;
        }
        Check(expectedStart == 701, "Final partial segment was not retained.");
#endif
        Console.WriteLine("PASS: profiler opt-in, segment rotation/flush, counters, state restoration, release-build guard.");
        return 0;
    }
}
