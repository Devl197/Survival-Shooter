using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WaveData", menuName = "Waves/Wave Data")]
public class WaveData : ScriptableObject
{
    [Header("Enemies")]
    public List<WaveEnemyEntry> enemies;

    [Header("Timing")]
    public float duration = 30f;
    [Tooltip("Globalna pauza između spawna — koristi se ako entry nema svoju")]
    public float defaultSpawnInterval = 1.5f;

    [Header("Spawn Mode")]
    [Tooltip("Sequential = spawna sve jednog tipa pa prelazi na sledeci\nInterleaved = naizmenicno mesanje tipova")]
    public SpawnMode spawnMode = SpawnMode.Sequential;

    public int TotalEnemyCount()
    {
        int total = 0;
        foreach (var entry in enemies) total += entry.count;
        return total;
    }
}

public enum SpawnMode { Sequential, Interleaved }