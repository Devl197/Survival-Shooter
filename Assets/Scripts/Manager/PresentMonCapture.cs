using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using UnityEngine;

[Serializable]
public class PresentMonRun
{
    public string toolVersion = "2.6.0";
    public string toolSha256 = "b2a706bc6ad475749e3b7e3409263aa1e6906d45bdcf993f6dbc0f660188f1af";
    public string scenario, allocation, replayFile, result = "Incomplete", error, session;
    public string scenarioConfiguration;
    public bool unityProfiler;
    public string unityProfilerDirectory;
    public string scene;
    public bool mainLightShadowsOverridden;
    public string mainLightShadows, mainLightName;
    public bool enemyCullingOverridden;
    public string animatorCullingMode;
    // These booleans describe the override only when enemyCullingOverridden is true.
    public bool updateWhenOffscreen, skinnedMotionVectors;
    public bool periodicPathUpdates;
    public float pathUpdateIntervalSeconds;
    public int processId, seed, enemyCount;
    public int vSyncCount, targetFrameRate;
    public int screenWidth, screenHeight;
    public bool fullscreen;
    public string qualityLevel;
    public float duration;
    public long frequency, startQpc, endQpc;
    public int filteredFrames;
}

// Windows-only capture bridge. Worker callbacks never access Unity objects.
public sealed class PresentMonCapture : IDisposable
{
    [DllImport("kernel32.dll")] static extern bool QueryPerformanceCounter(out long value);
    [DllImport("kernel32.dll")] static extern bool QueryPerformanceFrequency(out long value);
    public static long Now() { long value; if (!QueryPerformanceCounter(out value)) throw new InvalidOperationException("QPC unavailable"); return value; }
    public PresentMonRun Run = new PresentMonRun();
    public string DirectoryPath { get; private set; }
    public bool Ready { get { return ready; } }
    public bool Exited { get { return !processStarted || process.HasExited; } }
    public string Error { get { lock (gate) return error; } }
    volatile bool ready;
    readonly object gate = new object();
    Process process, stopper;
    StreamWriter raw, errors;
    string executable, error = "";
    int qpcColumn = -1, pidColumn = -1;
    bool closed;
    bool processStarted;

    public void Start(string path, string outputRoot)
    {
        executable = Path.GetFullPath(path);
        using (var stream = File.OpenRead(executable))
        using (var sha = SHA256.Create())
            if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() != Run.toolSha256)
                throw new InvalidDataException("PresentMon executable is not the pinned 2.6.0 release.");
        if (!QueryPerformanceFrequency(out Run.frequency) || Run.frequency <= 0) throw new InvalidOperationException("QPC frequency unavailable.");
        Run.processId = Process.GetCurrentProcess().Id;
        Run.session = "SurvivalShooter-" + Run.processId + "-" + Guid.NewGuid().ToString("N");
        DirectoryPath = Path.Combine(outputRoot, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
        raw = new StreamWriter(Path.Combine(DirectoryPath, "raw.csv"));
        errors = new StreamWriter(Path.Combine(DirectoryPath, "capture.log"));
        SaveMetadata();
        process = new Process { StartInfo = Info("--process_id " + Run.processId + " --session_name " + Run.session +
            " --qpc_time --v2_metrics --output_stdout --no_console_stats --terminate_on_proc_exit") };
        process.OutputDataReceived += (sender, args) => {
            if (args.Data == null) return;
            lock (gate)
            {
                if (closed) return;
                raw.WriteLine(args.Data);
                var columns = PresentMonCsv.Split(args.Data);
                if (columns.Contains("CPUStartQPC")) { qpcColumn = columns.IndexOf("CPUStartQPC"); pidColumn = columns.IndexOf("ProcessID"); }
                else if (qpcColumn >= 0 && pidColumn >= 0 && columns.Count > Math.Max(qpcColumn, pidColumn))
                {
                    long qpc; int pid;
                    if (long.TryParse(columns[qpcColumn], out qpc) && int.TryParse(columns[pidColumn], out pid) && pid == Run.processId) ready = true;
                }
            }
        };
        process.ErrorDataReceived += (sender, args) => {
            if (args.Data == null) return;
            lock (gate) { if (!closed) { errors.WriteLine(args.Data); error = args.Data; } }
        };
        process.Start(); processStarted = true; process.BeginOutputReadLine(); process.BeginErrorReadLine();
    }
    ProcessStartInfo Info(string arguments)
    {
        return new ProcessStartInfo(executable, arguments) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
    }
    public void MarkStart() { Run.startQpc = Now(); }
    public void MarkEnd(string result)
    {
        if (Run.endQpc != 0) return;
        Run.endQpc = Now(); Run.result = result;
    }
    public void RequestStop()
    {
        if (stopper != null || Exited) return;
        stopper = new Process { StartInfo = Info("--session_name " + Run.session + " --terminate_existing_session") };
        stopper.Start();
        stopper.BeginOutputReadLine(); stopper.BeginErrorReadLine();
    }
    public void Complete()
    {
        if (!Exited) throw new InvalidOperationException("Capture has not stopped.");
        if (processStarted) process.WaitForExit(); // Drain async stdout after process exit.
        CloseWriters();
        if (processStarted && process.ExitCode != 0) { Run.result = "CaptureFailed"; Run.error = Error; }
        if (Run.result == "Completed")
        {
            try
            {
                Run.filteredFrames = PresentMonCsv.Filter(Path.Combine(DirectoryPath, "raw.csv"),
                    Path.Combine(DirectoryPath, "measured.csv"), Run.processId, Run.startQpc, Run.endQpc, Run.frequency);
                if (Run.filteredFrames == 0) throw new InvalidDataException("No complete measured frames.");
            }
            catch (Exception e)
            {
                Run.result = "FilterFailed"; Run.error = e.Message;
                string partial = Path.Combine(DirectoryPath, "measured.csv");
                if (File.Exists(partial)) File.Move(partial, Path.Combine(DirectoryPath, "invalid-partial.csv"));
            }
        }
        SaveMetadata();
    }
    void CloseWriters() { lock (gate) { if (closed) return; closed = true; if (raw != null) raw.Dispose(); if (errors != null) errors.Dispose(); } }
    void SaveMetadata() { if (DirectoryPath != null) File.WriteAllText(Path.Combine(DirectoryPath, "session.json"), JsonUtility.ToJson(Run, true)); }
    public void Dispose()
    {
        if (processStarted && !process.HasExited)
        {
            MarkEnd(Run.result == "Incomplete" ? "Aborted" : Run.result); RequestStop();
            if (!process.WaitForExit(3000)) { process.Kill(); Run.result = "CaptureFailed"; Run.error = "Capture termination timed out."; }
        }
        if (DirectoryPath != null) { CloseWriters(); SaveMetadata(); }
        if (process != null) process.Dispose();
        if (stopper != null) stopper.Dispose();
    }
}

public static class PresentMonCsv
{
    public static List<string> Split(string line)
    {
        var result = new List<string>(); var field = new System.Text.StringBuilder(); bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"') { if (quoted && i + 1 < line.Length && line[i + 1] == '"') { field.Append('"'); i++; } else quoted = !quoted; }
            else if (c == ',' && !quoted) { result.Add(field.ToString()); field.Length = 0; }
            else field.Append(c);
        }
        result.Add(field.ToString()); return result;
    }
    public static int Filter(string source, string target, int pid, long start, long end, long frequency)
    {
        if (start <= 0 || end <= start || frequency <= 0) throw new InvalidDataException("Invalid capture bounds.");
        int count = 0; string swapChain = null;
        using (var reader = new StreamReader(source))
        using (var writer = new StreamWriter(target))
        {
            string header = reader.ReadLine();
            if (header == null) throw new InvalidDataException("Empty CSV.");
            var names = Split(header); int q = names.IndexOf("CPUStartQPC"), p = names.IndexOf("ProcessID"), f = names.IndexOf("FrameTime"), s = names.IndexOf("SwapChainAddress");
            if (q < 0 || p < 0 || f < 0 || s < 0) throw new InvalidDataException("Expected PresentMon 2.6.0 --v2_metrics --qpc_time columns.");
            writer.WriteLine(header); string line;
            while ((line = reader.ReadLine()) != null)
            {
                var fields = Split(line); long timestamp; int processId; decimal milliseconds;
                if (fields.Count != names.Count || !int.TryParse(fields[p], out processId) || processId != pid) continue;
                if (!long.TryParse(fields[q], out timestamp) || !decimal.TryParse(fields[f], NumberStyles.Float, CultureInfo.InvariantCulture, out milliseconds) || milliseconds <= 0) continue;
                // Conservative containment of the full CPU start-to-next-start interval.
                if (timestamp < start || (decimal)timestamp + milliseconds * frequency / 1000m > end) continue;
                if (swapChain == null) swapChain = fields[s];
                else if (swapChain != fields[s]) throw new InvalidDataException("Multiple swap chains in measured interval; select one before analysis.");
                writer.WriteLine(line); count++;
            }
        }
        return count;
    }
}

