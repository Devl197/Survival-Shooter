// Test double for orchestration only; never included in Assets or performance results.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;

class FakeGame
{
    static int Main(string[] args)
    {
        var json = new JavaScriptSerializer();
        int index = Array.IndexOf(args, "--benchmark-request");
        var request = json.Deserialize<Dictionary<string, object>>(File.ReadAllText(args[index + 1]));
        bool profilerOnly = Array.IndexOf(args, "--benchmark-profiler-log") >= 0;
        if (profilerOnly && (bool)request["captureWithPresentMon"])
            throw new Exception("Profiler diagnostics must not launch PresentMon.");
        string logPath = args[Array.IndexOf(args, "-logFile") + 1];
        File.WriteAllText(logPath, "Fake game started.\n");
        var graphics = (Dictionary<string, object>)request["graphics"];
        foreach (string option in new[] { "-screen-width", "-screen-height", "-screen-fullscreen", "-screen-quality" })
        {
            int argument = Array.IndexOf(args, option);
            string key = option == "-screen-width" ? "width" : option == "-screen-height" ? "height" : option == "-screen-quality" ? "quality" : "fullscreen";
            string expected = key == "fullscreen" ? ((bool)graphics[key] ? "1" : "0") : Convert.ToString(graphics[key]);
            if (argument < 0 || argument + 1 >= args.Length || args[argument + 1] != expected)
                throw new Exception("Missing or incorrect graphics argument: " + option);
        }
        string replay = (string)request["replayPath"];
        string mode = (string)json.Deserialize<Dictionary<string, object>>(File.ReadAllText(replay))["testMode"];
        if (mode == "timeout") { Thread.Sleep(10000); return 1; }
        if (mode == "missing") return 0;
        string directory = Path.Combine((string)request["resultsDirectory"], Guid.NewGuid().ToString("N"));
        if (!profilerOnly)
        {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "measured.csv"), mode == "bad-frame" ? "FrameTime\nNaN\n20\n" : "FrameTime\n10\n20\n");
        File.WriteAllText(Path.Combine(directory, "session.json"), json.Serialize(new {
            result = "Completed", filteredFrames = mode == "bad-count" ? 3 : 2,
            unityProfiler = Array.IndexOf(args, "--benchmark-profiler-log") >= 0,
            scene = request["scene"], scenario = request["scenario"], allocation = request["allocation"],
            processId = System.Diagnostics.Process.GetCurrentProcess().Id, replayFile = replay,
            screenWidth = Convert.ToInt32(graphics["width"]) + (mode == "bad-resolution" ? 1 : 0),
            screenHeight = graphics["height"], fullscreen = graphics["fullscreen"],
            qualityLevel = mode == "bad-quality" ? "Unexpected" : graphics["quality"],
            enemyCullingOverridden = true, animatorCullingMode = request["animatorCulling"],
            periodicPathUpdates = mode == "bad-settings" ? !(bool)request["periodicPathUpdates"] : (bool)request["periodicPathUpdates"],
            pathUpdateIntervalSeconds = request["pathUpdateIntervalSeconds"],
            updateWhenOffscreen = request["updateWhenOffscreen"], skinnedMotionVectors = request["skinnedMotionVectors"]
        }));
        }
        if (Array.IndexOf(args, "--benchmark-profiler-log") >= 0 && mode != "missing-profiler")
        {
            int profilerArgument = Array.IndexOf(args, "--benchmark-profiler-directory");
            if (profilerArgument < 0 || profilerArgument + 1 >= args.Length || !Path.IsPathRooted(args[profilerArgument + 1]))
                throw new Exception("Missing absolute Profiler directory argument.");
            string profilerDirectory = args[profilerArgument + 1];
            Directory.CreateDirectory(profilerDirectory);
            // Deliberately fake bytes for orchestration tests; not a Unity profile.
            File.WriteAllText(Path.Combine(profilerDirectory, "segment-0001.raw"), mode == "empty-profiler" ? "" : "test-double");
            File.WriteAllText(Path.Combine(profilerDirectory, "profiler.json"), json.Serialize(new {
                result = "Completed", scenario = request["scenario"], allocation = request["allocation"],
                framesPerSegment = 200, spawned = 420, removed = mode == "bad-profiler-counters" ? 119 : 120,
                poolExpansions = 0, targetFrameRate = 60, bufferBytes = 134217728, captureWithPresentMon = false,
                segments = new[] { new { file = "segment-0001.raw", startFrame = 10,
                    endFrame = mode == "oversized-profiler" ? 211 : 210, startSeconds = 0, endSeconds = mode == "stalled-profiler" ? 45 : 30 } }
            }));
            if (mode == "dropped-profiler") File.AppendAllText(logPath, "Skipping profile frame. Receiver can not keep up with the amount of data sent\n");
        }
        File.WriteAllText((string)request["reportPath"], json.Serialize(new {
            result = mode == "failed" ? "CaptureFailed" : "Completed", error = "", captureDirectory = profilerOnly ? null : directory
        }));
        return mode == "failed" ? 1 : 0;
    }
}
