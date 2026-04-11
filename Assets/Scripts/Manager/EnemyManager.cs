using System;
using System.Collections;
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

    void Start()
    {
        //Mengeksekusi fungs Spawn setiap beberapa detik sesuai dengan nilai spawnTime
        //InvokeRepeating("Spawn", spawnTime, spawnTime);
    }

    public void StartSpawning(WaveData data, Action onEnemyDied)
    {
        _spawnCoroutine = StartCoroutine(SpawnRoutine(data, onEnemyDied));
    }

    public IEnumerator SpawnRoutine(WaveData data, Action onEnemyDied)
    {
        int spawned = 0;

        while (spawned < data.enemyCount)
        {
            SpawnEnemy((int)data.enemyType, onEnemyDied);
            spawned++;
            yield return new WaitForSeconds(data.spawnInterval);
        }
    }
    private void SpawnEnemy(int enemyType, Action onEnemyDied)
    {
        if (playerHealth.currentHealth <= 0f)
        {
            return;
        }

        //Memduplikasi enemy
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