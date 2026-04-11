using System;
using UnityEngine;

[Serializable]
public class WaveEnemyEntry
{
    public EnemyType enemyType;
    public int count = 5;
    [Tooltip("Pauza između spawna za ovaj tip (0 = koristi globalnu vrednost)")]
    public float spawnInterval = 0f;
}

public enum EnemyType { Bunny, Bear, Hellephant }