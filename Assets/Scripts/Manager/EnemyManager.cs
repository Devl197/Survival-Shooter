using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    public PlayerHealth playerHealth;
    public float spawnTime = 3f;
    private Coroutine _spawnCoroutine;

    [SerializeField]
    EnemyFactory factory;
    IFactory Factory
    {
        get
        {
            return factory as IFactory;
        }
    }

    public void StartSpawning(WaveData data, Action onEnemyDied)
    {
        IEnumerator routine = data.spawnMode == SpawnMode.Sequential
            ? SpawnSequential(data, onEnemyDied)
            : SpawnInterleaved(data, onEnemyDied);

        _spawnCoroutine = StartCoroutine(routine);
    }

    // Spawna sve Basic-e, pa sve Fast-ove, pa sve Tank-ove...
    private IEnumerator SpawnSequential(WaveData data, Action onEnemyDied)
    {
        foreach (var entry in data.enemies)
        {
            float interval = entry.spawnInterval > 0f
                ? entry.spawnInterval
                : data.defaultSpawnInterval;

            for (int i = 0; i < entry.count; i++)
            {
                SpawnEnemy((int)entry.enemyType, onEnemyDied);
                yield return new WaitForSeconds(interval);
            }
        }
    }

    // Naizmenicno: Basic, Fast, Tank, Basic, Fast, Tank...
    private IEnumerator SpawnInterleaved(WaveData data, Action onEnemyDied)
    {
        // Napuni queue po jedan od svakog tipa, pa ponovi
        var queues = new Queue<WaveEnemyEntry>[data.enemies.Count];
        for (int i = 0; i < data.enemies.Count; i++)
        {
            queues[i] = new Queue<WaveEnemyEntry>();
            for (int j = 0; j < data.enemies[i].count; j++)
                queues[i].Enqueue(data.enemies[i]);
        }

        bool anyLeft = true;
        while (anyLeft)
        {
            anyLeft = false;
            foreach (var queue in queues)
            {
                if (queue.Count == 0) continue;

                var entry = queue.Dequeue();
                anyLeft = true;

                float interval = entry.spawnInterval > 0f
                    ? entry.spawnInterval
                    : data.defaultSpawnInterval;

                SpawnEnemy((int)entry.enemyType, onEnemyDied);
                yield return new WaitForSeconds(interval);
            }
        }
    }

    private void SpawnEnemy(int enemyType, Action onEnemyDied)
    {
        if (playerHealth.currentHealth <= 0f)
        {
            return;
        }

        Debug.Log($" Enemy of {(EnemyType)enemyType} is created!");
        var enemy = Factory.FactoryMethod(enemyType);
        var health = enemy.GetComponent<EnemyHealth>();
        health.SetDeathAction(onEnemyDied);
    }

    public void StopSpawning()
    {
        if (_spawnCoroutine != null)
        {
            StopCoroutine(_spawnCoroutine);
            _spawnCoroutine = null;
        }
    }

    void Spawn()
    {
        //Jika player telah mati maka tidak membuat enemy baru
        if (playerHealth.currentHealth <= 0f)
        {
            return;
        }

        //Mendapatkan nilai random
        int spawnEnemy = UnityEngine.Random.Range(0, 3);

        //Memduplikasi enemy
        var enemy = Factory.FactoryMethod(spawnEnemy);
    }
}