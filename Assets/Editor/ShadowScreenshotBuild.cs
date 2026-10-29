using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class ShadowScreenshotBuild
{
    [MenuItem("Tools/Benchmark/Build shadow screenshot Release")]
    public static void BuildFromMenu()
    {
        BuildAt(System.IO.Path.GetFullPath("Build/ShadowScreenshots/Survival Shooter.exe"));
    }

    public static void Build()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--shadow-build-path");
        if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Missing --shadow-build-path.");
        BuildAt(args[index + 1]);
    }

    static void BuildAt(string path)
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            locationPathName = path, target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Release build failed: " + report.summary.result);
    }
}
