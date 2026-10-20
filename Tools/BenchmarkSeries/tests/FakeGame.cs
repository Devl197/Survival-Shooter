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
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "measured.csv"), mode == "bad-frame" ? "FrameTime\nNaN\n20\n" : "FrameTime\n10\n20\n");
        File.WriteAllText(Path.Combine(directory, "session.json"), json.Serialize(new {
            result = "Completed", filteredFrames = mode == "bad-count" ? 3 : 2,
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
        File.WriteAllText((string)request["reportPath"], json.Serialize(new {
            result = mode == "failed" ? "CaptureFailed" : "Completed", error = "", captureDirectory = directory
        }));
        return mode == "failed" ? 1 : 0;
    }
}
