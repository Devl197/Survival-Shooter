using System;
using UnityEngine;

// Runtime-only benchmark override: keep the scene, light mode and baked data fixed.
public sealed class BenchmarkShadowOverride
{
    Light mainLight;
    LightShadows original, requested;
    public bool Applied { get; private set; }
    public string Mode { get { return Applied ? (requested == LightShadows.None ? "Off" : "Soft") : "Scene"; } }
    public string LightName { get { return mainLight != null ? mainLight.name : ""; } }

    public void Apply(string mode)
    {
        if (mode == "Scene") return;
        if (mode != "Off" && mode != "Soft")
            throw new ArgumentException("mainLightShadows must be Scene, Off or Soft.");
        if (Applied) throw new InvalidOperationException("Shadow override is already applied.");
        Light selected = null;
        foreach (Light light in UnityEngine.Object.FindObjectsOfType<Light>())
        {
            if (!light.isActiveAndEnabled || light.type != LightType.Directional) continue;
            if (selected != null) throw new InvalidOperationException("Shadow benchmark requires exactly one active directional light.");
            selected = light;
        }
        // Light.lightmapBakeType is editor-only in Unity 2018.3; the player exposes bakingOutput.
        if (selected == null || selected.bakingOutput.lightmapBakeType != LightmapBakeType.Realtime)
            throw new InvalidOperationException("Shadow benchmark requires one active realtime directional light.");
        if (mode == "Soft" && QualitySettings.shadows != ShadowQuality.All)
            throw new InvalidOperationException("Soft shadow benchmark requires a quality profile with hard and soft shadows enabled.");
        mainLight = selected;
        original = selected.shadows;
        requested = mode == "Off" ? LightShadows.None : LightShadows.Soft;
        mainLight.shadows = requested;
        Applied = true;
    }

    public bool Matches()
    {
        return !Applied || (mainLight != null && mainLight.isActiveAndEnabled &&
            mainLight.type == LightType.Directional && mainLight.bakingOutput.lightmapBakeType == LightmapBakeType.Realtime &&
            mainLight.shadows == requested && (requested != LightShadows.Soft || QualitySettings.shadows == ShadowQuality.All));
    }

    public void Restore()
    {
        if (Applied && mainLight != null) mainLight.shadows = original;
        Applied = false;
        mainLight = null;
    }
}
