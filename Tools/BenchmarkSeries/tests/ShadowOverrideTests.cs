using System;
using UnityEngine;

// Engine doubles: this executable tests override lifecycle, never rendering performance.
namespace UnityEngine
{
    public class Object
    {
        public static Light[] Lights = new Light[0];
        public static T[] FindObjectsOfType<T>() { return (T[])(object)Lights; }
    }
    public enum LightType { Directional, Point }
    public enum LightShadows { None, Hard, Soft }
    public enum LightmapBakeType { Realtime, Baked }
    public struct LightBakingOutput { public LightmapBakeType lightmapBakeType; }
    public enum ShadowQuality { Disable, HardOnly, All }
    public static class QualitySettings { public static ShadowQuality shadows = ShadowQuality.All; }
    public class Light : Object
    {
        public string name = "Main Light";
        public bool isActiveAndEnabled = true;
        public LightType type = LightType.Directional;
        public LightShadows shadows = LightShadows.Hard;
        public LightBakingOutput bakingOutput = new LightBakingOutput { lightmapBakeType = LightmapBakeType.Realtime };
    }
}

class ShadowOverrideTests
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Reject(Action action)
    {
        bool rejected = false;
        try { action(); } catch (ArgumentException) { rejected = true; } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Unsafe or invalid shadow configuration was accepted.");
    }
    static void Main()
    {
        var light = new Light();
        var point = new Light { type = LightType.Point };
        UnityEngine.Object.Lights = new[] { light, point };
        var control = new BenchmarkShadowOverride();
        control.Apply("Scene");
        Check(!control.Applied && light.shadows == LightShadows.Hard, "Default must preserve scene settings.");
        foreach (string mode in new[] { "Soft", "Off" })
        {
            control.Apply(mode);
            Check(control.Applied && control.Mode == mode && control.Matches(), "Override not applied.");
            Check(point.shadows == LightShadows.Hard, "Point light was modified.");
            Reject(() => control.Apply(mode));
            light.shadows = LightShadows.Hard;
            Check(!control.Matches(), "Changed shadows were not detected.");
            control.Restore();
            Check(light.shadows == LightShadows.Hard && !control.Applied, "Restore failed.");
            control.Restore();
        }
        control.Apply("Soft");
        QualitySettings.shadows = ShadowQuality.HardOnly;
        Check(!control.Matches(), "Changed quality was not detected.");
        control.Restore();
        Reject(() => control.Apply("Soft"));
        QualitySettings.shadows = ShadowQuality.All;
        light.bakingOutput = new LightBakingOutput { lightmapBakeType = LightmapBakeType.Baked };
        Reject(() => control.Apply("Off"));
        light.bakingOutput = new LightBakingOutput { lightmapBakeType = LightmapBakeType.Realtime };
        UnityEngine.Object.Lights = new[] { light, new Light() };
        Reject(() => control.Apply("Off"));
        UnityEngine.Object.Lights = new Light[0];
        Reject(() => control.Apply("Off"));
        Reject(() => control.Apply("soft"));
        Reject(() => control.Apply(null));
        Check(light.shadows == LightShadows.Hard, "Rejected override modified the light.");
        Console.WriteLine("PASS: shadow apply/restore, unchanged scene default, target selection, realtime/quality validation and drift detection.");
    }
}
