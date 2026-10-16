using System;
using System.Collections.Generic;
using UnityEngine;

public enum BenchmarkPattern { Continuous, Burst }


[CreateAssetMenu(fileName = "BenchmarkScenario", menuName = "Benchmark/Scenario")]
public class BenchmarkScenario : ScriptableObject
{
    public BenchmarkPattern pattern;
    public int enemyCount = 50;
    public int groupSize = 10;
    public float replacementInterval = 2f;
    public float initialLifetime = 5f;
    [Tooltip("Time for the death animation before the slot is replaced. Must be shorter than its replacement cycle.")]
    public float deathDuration = 2f;
    public int seed = 12345;
    [Tooltip("Weights corresponding to EnemyFactory.enemyPrefab indices.")]
    public int[] typeWeights = { 1, 1, 1 };
}

public struct BenchmarkReplacement
{
    public float time;
    public int slot;
    public int spawnPoint;
}

// Pure schedule generation: independent of frame rate, Unity Random and allocation mode.
public class BenchmarkSchedule
{
    public int[] types;
    public int[] initialPoints;
    public List<BenchmarkReplacement> replacements = new List<BenchmarkReplacement>();

    public static BenchmarkSchedule Build(int count, int group, float interval, float initialLifetime,
        float duration, int seed, int[] weights, int pointCount, BenchmarkPattern pattern)
    {
        if (count < 1 || group < 1 || group > count || pointCount < 1 ||
            !Positive(interval) || !Positive(initialLifetime) || !Positive(duration) ||
            weights == null || weights.Length == 0 || !Enum.IsDefined(typeof(BenchmarkPattern), pattern))
            throw new ArgumentException("Invalid benchmark count, group, timing, points or pattern.");
        long total = 0;
        foreach (int weight in weights)
        {
            if (weight < 0) throw new ArgumentException("Type weights must be nonnegative.");
            total += weight;
        }
        if (total < 1 || total > int.MaxValue) throw new ArgumentException("Invalid type weight sum.");
        double estimate = Math.Max(0, Math.Ceiling((duration - initialLifetime) / interval)) * group;
        if (estimate > 1000000 || count > 100000) throw new ArgumentException("Benchmark schedule is too large.");
        var schedule = new BenchmarkSchedule { types = new int[count], initialPoints = new int[count] };
        var random = new System.Random(seed);
        for (int slot = 0; slot < count; slot++)
        {
            int choice = random.Next((int)total);
            int type = 0;
            while (choice >= weights[type]) choice -= weights[type++];
            schedule.types[slot] = type;
            schedule.initialPoints[slot] = random.Next(pointCount);
        }
        int nextSlot = 0;
        for (int batch = 0; ; batch++)
        {
            double start = initialLifetime + (double)batch * interval;
            if (start >= duration) break;
            for (int member = 0; member < group; member++)
            {
                double time = start + (pattern == BenchmarkPattern.Continuous ? (double)member * interval / group : 0);
                if (time >= duration) break;
                schedule.replacements.Add(new BenchmarkReplacement {
                    time = (float)time, slot = nextSlot, spawnPoint = random.Next(pointCount)
                });
                nextSlot = (nextSlot + 1) % count;
            }
        }
        return schedule;
    }

    static bool Positive(float value) { return value > 0 && !float.IsInfinity(value) && !float.IsNaN(value); }
}
