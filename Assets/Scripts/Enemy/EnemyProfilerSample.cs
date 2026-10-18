using System;
using UnityEngine.Profiling;

// Value-type scope: no managed allocation, and EndSample runs even on exceptions.
// Unity removes BeginSample/EndSample calls when ENABLE_PROFILER is not defined.
internal struct EnemyProfilerSample : IDisposable
{
    public EnemyProfilerSample(string name)
    {
        Profiler.BeginSample(name);
    }

    public void Dispose()
    {
        Profiler.EndSample();
    }
}
