using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

// Keep each file below the Unity 2018.3 Profiler's 300-frame history.
internal sealed class BenchmarkProfilerCapture : IDisposable
{
    const int FramesPerSegment = 200;
    const int MaxSegments = 1024;
    [Serializable]
    sealed class Segment
    {
        public string file;
        [NonSerialized] public string path;
        public int startFrame, endFrame;
        public float startSeconds, endSeconds;
    }
    [Serializable]
    sealed class Manifest
    {
        public string result = "Incomplete";
        public int framesPerSegment = FramesPerSegment;
        public string unityVersion, scenario, allocation;
        public int spawned, removed, poolExpansions;
        public List<Segment> segments = new List<Segment>(MaxSegments);
    }
    readonly bool previousEnabled;
    readonly bool previousBinaryLog;
    readonly string previousLogFile;
    readonly string directory;
    readonly Manifest manifest;
    readonly Segment[] preparedSegments = new Segment[MaxSegments];
    float startedAt;
    Segment segment;
    bool recording, stopped;
    bool disposed;
    public string DirectoryPath { get { return directory; } }

    public static BenchmarkProfilerCapture StartIfRequested(string scenario, string allocation)
    {
        string[] args = Environment.GetCommandLineArgs();
        if (Array.IndexOf(args, "--benchmark-profiler-log") < 0) return null;
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        int index = Array.IndexOf(args, "--benchmark-profiler-directory");
        if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
            throw new InvalidOperationException("Profiler capture requires an absolute --benchmark-profiler-directory.");
        return new BenchmarkProfilerCapture(args[index + 1], scenario, allocation);
#else
        throw new InvalidOperationException("Unity Profiler capture requires a standalone Development Build. Rebuild the game.");
#endif
    }

    BenchmarkProfilerCapture(string path, string scenario, string allocation)
    {
        previousEnabled = Profiler.enabled;
        previousBinaryLog = Profiler.enableBinaryLog;
        previousLogFile = Profiler.logFile;
        if (previousEnabled || previousBinaryLog)
            throw new InvalidOperationException("Disable Autoconnect Profiler and other Profiler recording before this series.");
        directory = path;
        Directory.CreateDirectory(directory);
        manifest = new Manifest { unityVersion = Application.unityVersion, scenario = scenario, allocation = allocation };
        // Allocate bookkeeping before capture rather than adding GC.Alloc noise at every rotation.
        for (int i = 0; i < preparedSegments.Length; i++)
        {
            string file = "segment-" + (i + 1).ToString("D4", CultureInfo.InvariantCulture) + ".raw";
            preparedSegments[i] = new Segment { file = file, path = Path.Combine(directory, file) };
        }
        SaveManifest();
    }

    public void Begin()
    {
        if (recording || stopped) throw new InvalidOperationException("Profiler recording has already started or stopped.");
        startedAt = Time.realtimeSinceStartup;
        recording = true;
        OpenSegment();
    }

    // Called before benchmark events, so a Spawn/Despawn scope is never split.
    public void Advance()
    {
        if (!recording || Time.frameCount - segment.startFrame < FramesPerSegment) return;
        CloseSegment();
        OpenSegment();
    }

    void OpenSegment()
    {
        if (manifest.segments.Count == preparedSegments.Length)
            throw new InvalidOperationException("Profiler segment capacity exceeded.");
        segment = preparedSegments[manifest.segments.Count];
        segment.startFrame = Time.frameCount;
        segment.startSeconds = Time.realtimeSinceStartup - startedAt;
        manifest.segments.Add(segment);
        Profiler.logFile = segment.path;
        Profiler.enableBinaryLog = true;
        Profiler.enabled = true;
    }

    void CloseSegment()
    {
        Profiler.enabled = false;
        Profiler.enableBinaryLog = false;
        segment.endFrame = Time.frameCount;
        segment.endSeconds = Time.realtimeSinceStartup - startedAt;
    }

    public void Stop(string result, int spawned, int removed, int poolExpansions)
    {
        if (stopped) return;
        stopped = true;
        if (recording) CloseSegment();
        recording = false;
        manifest.result = result;
        manifest.spawned = spawned;
        manifest.removed = removed;
        manifest.poolExpansions = poolExpansions;
        SaveManifest();
    }

    void SaveManifest()
    {
        File.WriteAllText(Path.Combine(directory, "profiler.json"), JsonUtility.ToJson(manifest, true));
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { Stop("Aborted", 0, 0, 0); }
        finally
        {
            Profiler.enabled = false;
            Profiler.enableBinaryLog = false;
            Profiler.logFile = previousLogFile;
            Profiler.enableBinaryLog = previousBinaryLog;
            Profiler.enabled = previousEnabled;
        }
    }
}
