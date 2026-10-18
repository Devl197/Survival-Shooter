using System;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

// Opt-in binary recording avoids losing a 30-second run to the Profiler history limit.
internal sealed class BenchmarkProfilerCapture : IDisposable
{
    readonly bool previousEnabled;
    readonly bool previousBinaryLog;
    readonly string previousLogFile;
    bool disposed;

    public static BenchmarkProfilerCapture StartIfRequested()
    {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--benchmark-profiler-log") >= 0)
            return new BenchmarkProfilerCapture();
#endif
        return null;
    }

    BenchmarkProfilerCapture()
    {
        previousEnabled = Profiler.enabled;
        previousBinaryLog = Profiler.enableBinaryLog;
        previousLogFile = Profiler.logFile;
        if (previousBinaryLog)
            throw new InvalidOperationException("Another binary Profiler recording is already active.");
        try
        {
            string directory = Path.Combine(Application.persistentDataPath, "Profiler");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") +
                "-" + Guid.NewGuid().ToString("N") + ".raw");
            Profiler.enabled = false;
            Profiler.logFile = path;
            Profiler.enableBinaryLog = true;
            Profiler.enabled = true;
            Debug.Log("Benchmark Profiler recording: " + path);
        }
        catch { Dispose(); throw; }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Profiler.enabled = false;
        Profiler.enableBinaryLog = false;
        Profiler.logFile = previousLogFile;
        Profiler.enableBinaryLog = previousBinaryLog;
        Profiler.enabled = previousEnabled;
    }
}
