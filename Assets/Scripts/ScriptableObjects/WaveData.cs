using UnityEngine;

[CreateAssetMenu(fileName = "WaveData", menuName = "Waves/Wave Data")]
public class WaveData : ScriptableObject
{
    [Header("Enemy Settings")]
    //public GameObject enemyPrefab;
    public EnemyType enemyType;
    public int enemyCount = 10;

    [Header("Timing")]
    [Tooltip("Koliko sekundi traje ovaj wave (0 = dok svi ne budu ubijeni)")]
    public float duration = 30f;
    [Tooltip("Pauza između spawna neprijatelja u sekundama")]
    public float spawnInterval = 1.5f;
}

public enum EnemyType { Bunny, Bear, Hellephant }