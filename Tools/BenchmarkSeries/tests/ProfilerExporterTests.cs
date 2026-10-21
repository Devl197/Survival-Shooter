// Copy next to BenchmarkProfilerExporter.cs in an isolated Unity 2018.3 Editor project.
// Run with -executeMethod ProfilerExporterTests.Run. No game is started.
using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using UnityEditorInternal;
using UnityEditorInternal.Profiling;
using UnityEngine;

public static class ProfilerExporterTests
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Profiler exporter regression: " + message);
    }
    public static void Run()
    {
        long bytes;
        Check(BenchmarkProfilerExporter.TryAllocationBytes("size: 306", out bytes) && bytes == 306, "Native byte format");
        Check(BenchmarkProfilerExporter.TryAllocationBytes("size: 0", out bytes) && bytes == 0, "Zero allocation size");
        Check(!BenchmarkProfilerExporter.TryAllocationBytes("306 KB", out bytes), "Unknown units must not silently become bytes");
        Check(!BenchmarkProfilerExporter.TryAllocationBytes("", out bytes), "Missing bytes must be unknown");
        var samples = new Dictionary<string, BenchmarkProfilerExporter.Sample>();
        var replace = new BenchmarkProfilerExporter.Sample { key = "replace", marker = "Benchmark.ReplaceEnemy" };
        var spawn = new BenchmarkProfilerExporter.Sample { key = "spawn", marker = "Enemy.Spawner.Spawn" };
        var alloc = new BenchmarkProfilerExporter.Sample { key = "alloc", marker = "GC.Alloc", bytes = 80 };
        alloc.ancestors.Add("replace"); alloc.ancestors.Add("spawn");
        // A repeated ancestor identity must not count the allocation twice.
        alloc.ancestors.Add("spawn");
        samples.Add(replace.key, replace); samples.Add(spawn.key, spawn); samples.Add(alloc.key, alloc);
        BenchmarkProfilerExporter.FinalizeSamples(samples);
        Check(replace.bytes == 80 && spawn.bytes == 80 && alloc.bytes == 80, "Inclusive attribution without double counting leaves");
        BenchmarkProfilerExporter.FinalizeSamples(samples);
        Check(replace.bytes == 80 && spawn.bytes == 80, "Aggregation is repeatable");
        var unknown = new BenchmarkProfilerExporter.Sample { key = "unknown", marker = "GC.Alloc", allocationKnown = false };
        unknown.ancestors.Add("spawn"); samples.Add(unknown.key, unknown);
        BenchmarkProfilerExporter.FinalizeSamples(samples);
        Check(!spawn.allocationKnown && replace.allocationKnown, "Unknown size invalidates affected scope only");
        Check(BenchmarkProfilerExporter.Percentile95(new double[] { 1, 2, 3, 4, 100 }) == 100, "Nearest-rank p95");
        Check(BenchmarkProfilerExporter.Percentile95(new double[0]) == 0, "Empty percentile");
        Debug.Log("Profiler exporter regression tests PASSED.");
    }

    // Independent native cross-check: numeric RawHierarchy allocation columns versus raw tooltip bytes.
    public static void VerifyNativeAllocations()
    {
        Run();
        string[] args = Environment.GetCommandLineArgs();
        string path = args[Array.IndexOf(args, "-raw") + 1];
        ProfilerDriver.enabled = false;
        ProfilerDriver.ClearAllFrames();
        Check(ProfilerDriver.LoadProfile(path, false), "Native fixture load");
        long rawBytes = 0;
        double columnBytes = 0, spawnBytes = 0, despawnBytes = 0, replaceBytes = 0;
        int allocations = 0, spawnCalls = 0, despawnCalls = 0, replaceCalls = 0;
        using (var iterator = new ProfilerFrameDataIterator())
        {
            for (int f = ProfilerDriver.firstFrameIndex; f <= ProfilerDriver.lastFrameIndex; f++)
            {
                iterator.SetRoot(f, 0);
                Check(iterator.GetThreadName() == "Main Thread", "Fixture thread 0 is main");
                while (iterator.Next(true))
                {
                    if (iterator.name != "GC.Alloc") continue;
                    long bytes;
                    Check(BenchmarkProfilerExporter.TryAllocationBytes(iterator.extraTooltipInfo, out bytes), "All native allocation sizes parsed");
                    rawBytes += bytes; allocations++;
                }
                using (var property = new ProfilerProperty())
                {
                    property.SetRoot(f, ProfilerColumn.DontSort, ProfilerViewType.RawHierarchy);
                    property.onlyShowGPUSamples = false;
                    while (property.Next(true))
                    {
                        double bytes = property.GetColumnAsSingle(ProfilerColumn.GCMemory);
                        if (property.propertyName == "GC.Alloc") columnBytes += bytes;
                        if (property.propertyName == "Enemy.Spawner.Spawn") { spawnBytes += bytes; spawnCalls++; }
                        if (property.propertyName == "Enemy.Spawner.Despawn") { despawnBytes += bytes; despawnCalls++; }
                        if (property.propertyName == "Benchmark.ReplaceEnemy") { replaceBytes += bytes; replaceCalls++; }
                    }
                }
            }
        }
        Check(rawBytes == columnBytes, "Raw tooltip bytes match native numeric leaf columns");
        Debug.Log(String.Format(CultureInfo.InvariantCulture,
            "NATIVE_CROSSCHECK allocBytes={0} allocCalls={1} spawnBytes={2} spawnCalls={3} despawnBytes={4} despawnCalls={5} replaceBytes={6} replaceCalls={7}",
            rawBytes, allocations, spawnBytes, spawnCalls, despawnBytes, despawnCalls, replaceBytes, replaceCalls));
        ProfilerDriver.ClearAllFrames();
    }
}
