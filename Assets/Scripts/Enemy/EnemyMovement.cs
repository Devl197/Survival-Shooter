using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    [SerializeField] EnemyPathUpdateSettings pathUpdateSettings;
    public bool PeriodicPathUpdates { get { return pathUpdateSettings != null && pathUpdateSettings.periodicUpdates; } }
    public float PathUpdateInterval { get { return pathUpdateSettings != null ? pathUpdateSettings.Interval : 0f; } }
    float lastPathUpdate;
    bool hasRequestedPath;
    Transform player;
    PlayerHealth playerHealth;
    EnemyHealth enemyHealth;
    UnityEngine.AI.NavMeshAgent nav;


    void Awake()
    {
        //Cari game object with tag player
        player = GameObject.FindGameObjectWithTag("Player").transform;

        //Mendapatkan componen reference
        playerHealth = player.GetComponent<PlayerHealth>();
        enemyHealth = GetComponent<EnemyHealth>();
        nav = GetComponent<UnityEngine.AI.NavMeshAgent>();
    }


    void OnEnable()
    {
        // Also reset when an enemy is reused from the pool.
        hasRequestedPath = false;
    }

    void Update()
    {
        //Pindah ke player position
        if (enemyHealth.currentHealth > 0 && playerHealth.currentHealth > 0)
        {
            if (!nav.isActiveAndEnabled || !nav.isOnNavMesh) return;

            bool periodic = PeriodicPathUpdates;
            if (!periodic || !hasRequestedPath ||
                Time.time - lastPathUpdate >= pathUpdateSettings.Interval)
            {
                nav.SetDestination(player.position);
                lastPathUpdate = Time.time;
                hasRequestedPath = true;
            }
        }
        else //Stop moving
        {
            nav.enabled = false;
        }
    }
}
