using System;
using System.Collections;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    [SerializeField] private WaveData[] waves;
    [SerializeField] private EnemyManager spawner;

    public event Action<int> OnWaveStarted;
    public event Action<int> OnWaveCompleted;

    public int CurrentWaveIndex { get; private set; } = -1;
    public WaveState State { get; private set; } = WaveState.Idle;

    private Coroutine _activeWave;
    private int _remainingEnemies;

    void Start()
    {
        StartNextWave();
    }

    public void StartNextWave()
    {
        if (State != WaveState.Idle) return;

        CurrentWaveIndex++;
        if (CurrentWaveIndex >= waves.Length)
        {
            Debug.Log("Svi wave-ovi završeni!");
            return;
        }

        _activeWave = StartCoroutine(RunWave(waves[CurrentWaveIndex]));
    }

    private IEnumerator RunWave(WaveData data)
    {
        State = WaveState.Spawning;
        _remainingEnemies = data.enemyCount;

        OnWaveStarted?.Invoke(CurrentWaveIndex);
        Debug.Log($"Wave {CurrentWaveIndex + 1} počeo — {data.enemyType}, {data.enemyCount} neprijatelja");

        // Pokreni spawning
        spawner.StartSpawning(data, OnEnemyDied);

        // Čekaj dok ne istekne duration ILI svi neprijatelji budu eliminisani
        float elapsed = 0f;
        bool hasDuration = data.duration > 0f;

        while (_remainingEnemies > 0)
        {
            if (hasDuration)
            {
                elapsed += Time.deltaTime;
                if (elapsed >= data.duration) break;
            }
            yield return null;
        }

        EndWave();
    }

    private void OnEnemyDied()
    {
        _remainingEnemies = Mathf.Max(0, _remainingEnemies - 1);
    }

    private void EndWave()
    {
        State = WaveState.Idle;
        spawner.StopSpawning();
        OnWaveCompleted?.Invoke(CurrentWaveIndex);
        Debug.Log($"Wave {CurrentWaveIndex + 1} završen!");
    }
}

public enum WaveState { Idle, Spawning, WaitingForClear }