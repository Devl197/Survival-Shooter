// Unity 2018.3 Editor only. Reads native .raw files through the matching Unity API.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public static class BenchmarkProfilerExporter
{
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    static readonly Regex AllocationSize = new Regex(@"^size:\s*(\d+)\s*$", RegexOptions.CultureInvariant);
    const string Replace = "Benchmark.ReplaceEnemy";
    const string Spawn = "Enemy.Spawner.Spawn";
    const string Despawn = "Enemy.Spawner.Despawn";
    const string Collect = "GC.Collect";

    [Serializable] public sealed class Segment
    {
        public string file;
        public int startFrame, endFrame;
        public float startSeconds, endSeconds;
    }
    [Serializable] public sealed class Manifest
    {
        public string result, unityVersion, scenario, allocation;
        public int spawned, removed, poolExpansions, framesPerSegment;
        public Segment[] segments;
    }

    // Data model and aggregation are independent of the native reader, for regression tests.
    public sealed class Sample
    {
        public string key, marker, thread, segment, path;
        public int frame, threadIndex, ordinal;
        public double absoluteSeconds, milliseconds, selfMilliseconds;
        public long bytes;
        public bool allocationKnown = true, boundary, mainThread, nestedCollector;
        public readonly List<string> ancestors = new List<string>();
    }
    sealed class Run
    {
        public string directory, id, error = "";
        public Manifest manifest;
        public double origin = double.PositiveInfinity;
        public int segmentCount, frameParts, duplicateSamples, unknownAllocations;
        public readonly Dictionary<string, Sample> samples = new Dictionary<string, Sample>();
        public readonly List<string> warnings = new List<string>();
        public string Status { get { return error.Length > 0 ? "Invalid" : warnings.Count > 0 ? "Review" : "Complete"; } }
    }
    sealed class StackEntry
    {
        public int depth;
        public Sample sample;
        public double childMilliseconds;
    }

    [MenuItem("Tools/Benchmark/Export Profiler recordings to CSV")]
    public static void ExportMenu()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before exporting.");
        string input = EditorUtility.OpenFolderPanel("Select series folder or run profiler folder", "", "");
        if (String.IsNullOrEmpty(input)) return;
        string destination = EditorUtility.OpenFolderPanel("Select destination for a new export folder", input, "");
        if (String.IsNullOrEmpty(destination)) return;
        string output = Path.Combine(destination, "ProfilerExport-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        try
        {
            Export(input, output);
            EditorUtility.DisplayDialog("Profiler export", "Export saved to:\n" + output + "\n\nRead Report.md and check Status before comparing results.", "OK");
            EditorUtility.RevealInFinder(Path.Combine(output, "Report.md"));
        }
        catch (Exception exception) { Debug.LogException(exception); EditorUtility.DisplayDialog("Profiler export failed", exception.Message, "OK"); }
    }

    // Example: -executeMethod BenchmarkProfilerExporter.ExportBatch -benchmarkProfilerInput <folder>
    //          -benchmarkProfilerOutput <new-folder>. Existing output directories are never overwritten.
    public static void ExportBatch()
    {
        Export(Argument("-benchmarkProfilerInput"), Argument("-benchmarkProfilerOutput"));
    }
    static string Argument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Missing " + name);
        return args[index + 1];
    }

    public static void Export(string input, string output)
    {
        if (!Application.unityVersion.StartsWith("2018.3.", StringComparison.Ordinal))
            throw new InvalidOperationException("Use Unity 2018.3, preferably the same patch as the recording.");
        input = Path.GetFullPath(input); output = Path.GetFullPath(output);
        if (!Directory.Exists(input)) throw new DirectoryNotFoundException(input);
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Choose a new output folder: " + output);
        string[] manifests = Directory.GetFiles(input, "profiler.json", SearchOption.AllDirectories);
        if (manifests.Length == 0) throw new InvalidOperationException("No profiler.json manifests found in " + input);
        Array.Sort(manifests, StringComparer.OrdinalIgnoreCase);
        Directory.CreateDirectory(output);
        string incomplete = Path.Combine(output, "INCOMPLETE.txt");
        File.WriteAllText(incomplete, "Export is not finished. Do not use partial CSV files as a complete report.");
        bool previousEnabled = ProfilerDriver.enabled;
        var runs = new List<Run>();
        try
        {
            ProfilerDriver.enabled = false;
            using (var events = Csv(Path.Combine(output, "events.csv")))
            using (var frames = Csv(Path.Combine(output, "frame-parts.csv")))
            using (var metrics = Csv(Path.Combine(output, "markers.csv")))
            using (var summary = Csv(Path.Combine(output, "summary.csv")))
            {
                Row(events, "run", "scenario", "allocation", "segment", "loadedFrame", "thread", "marker", "phase", "secondsFromFirstSample", "durationMs", "selfMs", "gcAllocBytes", "allocationKnown", "boundaryFrame", "path");
                Row(frames, "run", "segment", "loadedFrame", "thread", "frameStartSeconds", "framePartMs", "boundaryFrame", "note");
                Row(metrics, "run", "scenario", "allocation", "marker", "calls", "totalMs", "meanMsPerCall", "maxCallMs", "p95CallMs", "selfTotalMs", "gcAllocBytes", "meanBytesPerCall", "worstSegment", "worstLoadedFrame", "status");
                Row(summary, "run", "scenario", "allocation", "status", "segments", "uniqueSamples", "duplicateSamplesRemoved", "expectedReplacements", "replacementCalls", "spawnCalls", "despawnCalls", "spawnTotalMs", "spawnMeanMs", "spawnMaxMs", "despawnTotalMs", "despawnMeanMs", "despawnMaxMs", "replacementTotalMs", "replacementMaxMs", "maxReplacementBatchMs", "spawnAllocBytes", "despawnAllocBytes", "replacementAllocBytes", "allThreadsAllocBytes", "mainThreadGCEvents", "mainThreadGCTotalMs", "mainThreadGCMaxMs", "gcStatus", "notes", "source");
                for (int index = 0; index < manifests.Length; index++)
                {
                    var run = new Run { directory = Path.GetDirectoryName(manifests[index]) };
                    run.id = "run-" + (index + 1).ToString("D4") + "-" + Path.GetFileName(run.directory);
                    runs.Add(run);
                    try
                    {
                        run.manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifests[index]));
                        ValidateManifest(run);
                        for (int s = 0; s < run.manifest.segments.Length; s++)
                        {
                            Progress((float)index / manifests.Length, run.id + ": " + run.manifest.segments[s].file);
                            ReadSegment(run, run.manifest.segments[s], frames);
                            run.segmentCount++;
                        }
                        FinalizeSamples(run.samples);
                        ValidateCoverage(run);
                        WriteEvents(run, events);
                        WriteMetrics(run, metrics);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception exception) { run.error = exception.Message; Debug.LogWarning(run.id + ": " + exception.Message); }
                    WriteSummary(run, summary);
                }
            }
            WriteReport(runs, output);
            File.Delete(incomplete);
            Debug.Log("Profiler CSV export: " + output + "; runs=" + runs.Count);
        }
        finally { ProfilerDriver.ClearAllFrames(); ProfilerDriver.enabled = previousEnabled; EditorUtility.ClearProgressBar(); }
    }

    static void Progress(float progress, string message)
    {
        if (!Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar("Profiler CSV export", message, progress))
            throw new OperationCanceledException("Export cancelled. Partial output must not be used as a complete export.");
    }
    static void ValidateManifest(Run run)
    {
        Manifest m = run.manifest;
        if (m == null || m.segments == null || m.segments.Length == 0) throw new InvalidDataException("Empty or invalid manifest.");
        if (m.unityVersion != Application.unityVersion) run.warnings.Add("Editor version differs from recording: " + m.unityVersion);
        if (m.result != "Completed") run.warnings.Add("Recording result: " + m.result);
        if (m.removed <= 0) run.warnings.Add("Manifest contains no expected replacements.");
        if (m.poolExpansions != 0) run.warnings.Add("Pool expansions: " + m.poolExpansions);
        string logName = Path.GetFileName(run.directory).Replace("-profiler", "") + ".player.log";
        string log = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(run.directory)), "_automation", logName);
        if (!File.Exists(log)) run.warnings.Add("Player log unavailable; dropped-frame warnings could not be checked.");
        else
        {
            string text = File.ReadAllText(log);
            if (text.Contains("Skipping profile frame") || text.Contains("Receiver can not keep up"))
                run.warnings.Add("Player log reports dropped Profiler data.");
        }
        if (m.segments[m.segments.Length - 1].endSeconds > 32) run.warnings.Add("Capture lasted over 32 wall-clock seconds; inspect recording stalls.");
    }

    static void ReadSegment(Run run, Segment segment, StreamWriter frames)
    {
        string path = Path.GetFullPath(Path.Combine(run.directory, segment.file ?? ""));
        string root = Path.GetFullPath(run.directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(path) != segment.file)
            throw new InvalidDataException("Segment must be a file inside its run folder.");
        if (!File.Exists(path) || new FileInfo(path).Length == 0) throw new InvalidDataException("Missing or empty segment: " + path);
        ProfilerDriver.ClearAllFrames();
        if (!ProfilerDriver.LoadProfile(path, false)) throw new InvalidDataException("Unity could not load " + path);
        int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
        if (first < 0 || last < first) throw new InvalidDataException("Segment contains no frames: " + path);
        if (last - first + 1 >= ProfilerDriver.maxHistoryLength)
            run.warnings.Add(segment.file + ": history limit reached; beginning may have been truncated.");
        using (var iterator = new ProfilerFrameDataIterator())
        {
            for (int frame = first; frame <= last; frame++)
            {
                double frameStart = iterator.GetFrameStartS(frame);
                if (Double.IsNaN(frameStart) || Double.IsInfinity(frameStart)) throw new InvalidDataException("Invalid frame timestamp.");
                run.origin = Math.Min(run.origin, frameStart);
                int threads = iterator.GetThreadCount(frame);
                for (int thread = 0; thread < threads; thread++)
                {
                    iterator.SetRoot(frame, thread);
                    string threadName = iterator.GetThreadName() ?? ("Thread " + thread);
                    bool main = threadName == "Main Thread";
                    bool boundary = frame == first || frame == last;
                    if (main)
                    {
                        run.frameParts++;
                        Row(frames, run.id, segment.file, frame, threadName, frameStart, iterator.frameTimeMS, boundary,
                            boundary ? "Partial boundary frame possible; not an end-to-end frame-time measurement" : "Profiler frame time includes instrumentation and FPS waiting");
                    }
                    var stack = new List<StackEntry>();
                    var occurrences = new Dictionary<string, int>();
                    while (iterator.Next(true))
                    {
                        while (stack.Count > 0 && stack[stack.Count - 1].depth >= iterator.depth) Pop(stack);
                        string marker = iterator.name ?? "";
                        double duration = iterator.durationMS;
                        var entry = new StackEntry { depth = iterator.depth };
                        if (stack.Count > 0) stack[stack.Count - 1].childMilliseconds += duration;
                        if (IsTracked(marker))
                        {
                            long bytes = 0;
                            bool known = marker != "GC.Alloc" || TryAllocationBytes(iterator.extraTooltipInfo, out bytes);
                            string samplePath = iterator.path ?? marker;
                            double absolute = frameStart + iterator.startTimeMS / 1000.0;
                            string identity = thread + "|" + threadName + "|" + marker + "|" + samplePath + "|" +
                                Math.Round(absolute * 1000000.0).ToString("0", Invariant) + "|" + bytes;
                            int ordinal; occurrences.TryGetValue(identity, out ordinal); occurrences[identity] = ordinal + 1;
                            var sample = new Sample { key = identity + "|" + ordinal, ordinal = ordinal, marker = marker,
                                thread = threadName, threadIndex = thread, segment = segment.file, frame = frame,
                                absoluteSeconds = absolute, milliseconds = duration, path = samplePath, bytes = bytes,
                                allocationKnown = known, boundary = boundary, mainThread = main };
                            foreach (StackEntry ancestor in stack)
                            {
                                if (ancestor.sample == null) continue;
                                sample.ancestors.Add(ancestor.sample.key);
                                if (ancestor.sample.marker == Collect) sample.nestedCollector = true;
                            }
                            entry.sample = sample;
                            Sample previous;
                            if (run.samples.TryGetValue(sample.key, out previous))
                            {
                                run.duplicateSamples++;
                                if (previous.milliseconds > sample.milliseconds) entry.sample = previous;
                                else run.samples[sample.key] = sample;
                            }
                            else run.samples.Add(sample.key, sample);
                        }
                        stack.Add(entry);
                    }
                    while (stack.Count > 0) Pop(stack);
                }
            }
        }
    }
    static void Pop(List<StackEntry> stack)
    {
        StackEntry entry = stack[stack.Count - 1];
        if (entry.sample != null) entry.sample.selfMilliseconds = Math.Max(0, entry.sample.milliseconds - entry.childMilliseconds);
        stack.RemoveAt(stack.Count - 1);
    }
    static bool IsTracked(string marker)
    {
        return marker == "GC.Alloc" || marker == Collect || marker.StartsWith("Enemy.", StringComparison.Ordinal) || marker.StartsWith("Benchmark.", StringComparison.Ordinal);
    }
    public static bool TryAllocationBytes(string tooltip, out long bytes)
    {
        Match match = AllocationSize.Match(tooltip ?? "");
        bytes = 0;
        return match.Success && Int64.TryParse(match.Groups[1].Value, NumberStyles.None, Invariant, out bytes);
    }
    public static void FinalizeSamples(Dictionary<string, Sample> samples)
    {
        foreach (Sample sample in samples.Values) if (sample.marker != "GC.Alloc") sample.bytes = 0;
        foreach (Sample allocation in samples.Values.Where(s => s.marker == "GC.Alloc"))
        {
            foreach (string key in allocation.ancestors.Distinct())
            {
                Sample ancestor;
                if (!samples.TryGetValue(key, out ancestor)) continue;
                ancestor.bytes += allocation.bytes;
                ancestor.allocationKnown &= allocation.allocationKnown;
            }
        }
    }
    static List<Sample> Select(Run run, string marker)
    {
        return run.samples.Values.Where(s => s.marker == marker && (marker != Collect || (s.mainThread && !s.nestedCollector))).OrderBy(s => s.absoluteSeconds).ToList();
    }
    static void ValidateCoverage(Run run)
    {
        foreach (string marker in new[] { Replace, Spawn, Despawn })
        {
            int actual = Select(run, marker).Count;
            if (actual != run.manifest.removed) run.warnings.Add(marker + ": expected " + run.manifest.removed + ", found " + actual);
        }
        run.unknownAllocations = run.samples.Values.Count(s => s.marker == "GC.Alloc" && !s.allocationKnown);
        if (run.unknownAllocations != 0) run.warnings.Add("Unparsed allocation sizes: " + run.unknownAllocations + "; allocation totals are incomplete.");
    }
    static void WriteEvents(Run run, StreamWriter writer)
    {
        foreach (Sample s in run.samples.Values.OrderBy(s => s.absoluteSeconds).ThenBy(s => s.threadIndex).ThenBy(s => s.ordinal))
        {
            bool replacement = s.marker == Replace || s.ancestors.Any(k => run.samples.ContainsKey(k) && run.samples[k].marker == Replace);
            Row(writer, run.id, run.manifest.scenario, run.manifest.allocation, s.segment, s.frame, s.thread, s.marker,
                replacement ? "Replacement" : "OtherGameplay", s.absoluteSeconds - run.origin, s.milliseconds, s.selfMilliseconds,
                s.allocationKnown ? (object)s.bytes : "", s.allocationKnown, s.boundary, s.path);
        }
    }
    static double Total(List<Sample> samples) { return samples.Sum(s => s.milliseconds); }
    static double Mean(List<Sample> samples) { return samples.Count == 0 ? 0 : Total(samples) / samples.Count; }
    static double Max(List<Sample> samples) { return samples.Count == 0 ? 0 : samples.Max(s => s.milliseconds); }
    static object Bytes(List<Sample> samples) { return samples.Any(s => !s.allocationKnown) ? (object)"" : samples.Sum(s => s.bytes); }
    public static double Percentile95(IEnumerable<double> values)
    {
        double[] sorted = values.OrderBy(v => v).ToArray();
        return sorted.Length == 0 ? 0 : sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * .95) - 1)];
    }
    static void WriteMetrics(Run run, StreamWriter writer)
    {
        string[] markers = run.samples.Values.Select(s => s.marker).Concat(new[] { Replace, Spawn, Despawn, Collect }).Distinct().OrderBy(s => s).ToArray();
        foreach (string marker in markers)
        {
            List<Sample> samples = Select(run, marker);
            Sample worst = samples.OrderByDescending(s => s.milliseconds).FirstOrDefault();
            object bytes = Bytes(samples);
            Row(writer, run.id, run.manifest.scenario, run.manifest.allocation, marker, samples.Count,
                Total(samples), Mean(samples), Max(samples), Percentile95(samples.Select(s => s.milliseconds)),
                samples.Sum(s => s.selfMilliseconds), bytes,
                bytes is string || samples.Count == 0 ? (object)"" : Convert.ToDouble(bytes) / samples.Count,
                worst == null ? "" : worst.segment, worst == null ? (object)"" : worst.frame,
                marker == Collect && samples.Count == 0 ? "NotObserved" : samples.Count == 0 ? "NotObserved" : run.Status);
        }
    }
    static double MaxBatch(Run run)
    {
        // Frame IDs are local to each loaded segment. Custom replacement scopes are never split by the recorder.
        return Select(run, Replace).GroupBy(s => s.segment + "|" + s.frame).Select(g => g.Sum(s => s.milliseconds)).DefaultIfEmpty(0).Max();
    }
    static void WriteSummary(Run run, StreamWriter writer)
    {
        Manifest m = run.manifest ?? new Manifest();
        if (run.error.Length > 0)
        {
            var fields = new object[30]; fields[0] = run.id; fields[1] = m.scenario; fields[2] = m.allocation;
            fields[3] = "Invalid"; fields[28] = run.error; fields[29] = run.directory;
            Row(writer, fields); return;
        }
        List<Sample> spawn = Select(run, Spawn), despawn = Select(run, Despawn), replace = Select(run, Replace), gc = Select(run, Collect);
        Row(writer, run.id, m.scenario, m.allocation, run.Status, run.segmentCount, run.samples.Count, run.duplicateSamples,
            m.removed, replace.Count, spawn.Count, despawn.Count, Total(spawn), Mean(spawn), Max(spawn), Total(despawn), Mean(despawn), Max(despawn),
            Total(replace), Max(replace), MaxBatch(run), Bytes(spawn), Bytes(despawn), Bytes(replace),
            Bytes(run.samples.Values.Where(s => s.marker == "GC.Alloc").ToList()), gc.Count, Total(gc), Max(gc),
            gc.Count == 0 ? "NotObserved" : "Observed", String.Join("; ", run.warnings.ToArray()), run.directory);
    }
    static void WriteReport(List<Run> runs, string output)
    {
        var report = new StringBuilder("# Profiler export\n\n");
        report.Append("Editor: ").Append(Application.unityVersion).Append(". Times are milliseconds; allocations are bytes.\n\n");
        report.Append("| Scenario | Mode | Status | Replacements | Spawn mean/max ms | Despawn mean/max ms | Replacement bytes | GC events / total ms |\n|---|---|---|---:|---:|---:|---:|---:|\n");
        foreach (Run run in runs)
        {
            Manifest m = run.manifest ?? new Manifest();
            List<Sample> spawn = Select(run, Spawn), despawn = Select(run, Despawn), gc = Select(run, Collect);
            report.Append("| ").Append(m.scenario).Append(" | ").Append(m.allocation).Append(" | ").Append(run.Status).Append(" | ");
            if (run.error.Length > 0) report.Append("— | — | — | — | — |\n");
            else report.Append(Select(run, Replace).Count).Append(" | ").Append(F(Mean(spawn))).Append(" / ").Append(F(Max(spawn)))
                .Append(" | ").Append(F(Mean(despawn))).Append(" / ").Append(F(Max(despawn))).Append(" | ").Append(Bytes(Select(run, Replace)))
                .Append(" | ").Append(gc.Count == 0 ? "Not observed" : gc.Count + " / " + F(Total(gc))).Append(" |\n");
        }
        report.Append("\n## Reading the export\n\n")
            .Append("- summary.csv: one row per recording. Compare Complete rows; Review requires checking the notes. Invalid rows have no totals.\n")
            .Append("- markers.csv: calls, inclusive total/mean/max/p95 duration, self time and inclusive attributed allocation bytes per marker. Do not add parent and child totals.\n")
            .Append("- events.csv: individual custom markers, GC.Alloc and GC.Collect, source segment and local loaded frame for inspection. GC.Alloc duration is allocation work, not collector pause.\n")
            .Append("- frame-parts.csv: main-thread frame metadata. Boundary frames can be partial; this is not a replacement for PresentMon frame-time data.\n")
            .Append("- Initial spawning and pool prewarming occur BEFORE capture in this project; expected Spawn/Despawn/Replace calls equal manifest.removed (120 for the supplied series), not manifest.spawned (420).\n")
            .Append("- Allocation totals sum only GC.Alloc leaf samples across all recorded threads. Scope bytes include allocations in nested samples. Unknown size formats produce blank totals, never a false zero.\n")
            .Append("- GC summary uses outermost GC.Collect samples on Main Thread. Other thread samples remain in events.csv. NotObserved means no collector event was recorded, not zero future GC cost.\n")
            .Append("- DestroyRequest measures a request; deferred native destruction can occur outside it. Profiler overhead, segment-switch overhead and FPS waiting are not Spawn/Despawn costs unless inside those scopes.\n")
            .Append("- Timing origin is the first recorded frame, not exact replay time. Segment/frame references are authoritative.\n")
            .Append("- Overlap identity uses thread, path, marker, absolute timestamp rounded to microseconds, size and occurrence ordinal. Duplicate samples retain the longer duration. Boundary recording gaps cannot be reconstructed; coverage checks and player logs help detect missing data.\n")
            .Append("- A Complete result checks known markers and logs; it cannot prove that every unrelated background sample was recorded.\n");
        foreach (Run run in runs)
        {
            report.Append("\n## ").Append(run.id).Append("\n\nSource: ").Append(run.directory).Append("\n\n");
            if (run.error.Length > 0) report.Append("ERROR: ").Append(run.error).Append("\n");
            foreach (string warning in run.warnings) report.Append("- ").Append(warning).Append("\n");
        }
        File.WriteAllText(Path.Combine(output, "Report.md"), report.ToString(), new UTF8Encoding(false));
    }
    static string F(double value) { return value.ToString("0.######", Invariant); }
    static StreamWriter Csv(string path) { return new StreamWriter(path, false, new UTF8Encoding(true)); }
    static void Row(StreamWriter writer, params object[] values)
    {
        writer.WriteLine(String.Join(",", values.Select(v =>
        {
            string text = v == null ? "" : v is double ? ((double)v).ToString("R", Invariant) : v is float ? ((float)v).ToString("R", Invariant) : Convert.ToString(v, Invariant);
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }).ToArray()));
    }
}
