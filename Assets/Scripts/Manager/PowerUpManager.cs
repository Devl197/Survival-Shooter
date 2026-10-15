using UnityEngine;

public class PowerUpManager : MonoBehaviour
{
    public PlayerHealth playerHealth;
    public float spawnTime = 7f;
    bool benchmarkDisabled;

    public void DisableForBenchmark()
    {
        benchmarkDisabled = true;
        CancelInvoke();
        enabled = false;
    }

    [SerializeField]
    PowerUpFactory factory;
    IFactory Factory
    {
        get
        {
            return factory as IFactory;
        }
    }

    void Start()
    {
        if (benchmarkDisabled) return;
        //Mengeksekusi fungs Spawn setiap beberapa detik sesuai dengan nilai spawnTime
        InvokeRepeating("Spawn", spawnTime, spawnTime);
    }


    void Spawn()
    {
        if (benchmarkDisabled) return;
        //Jika player telah mati maka tidak membuat enemy baru
        if (playerHealth.currentHealth <= 0f)
        {
            return;
        }

        //Mendapatkan nilai random
        int spawnPoint = Random.Range(0, 3);

        //Memduplikasi enemy
        Factory.FactoryMethod(spawnPoint);
    }
}
