using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public int startingHealth = 100;
    public int currentHealth;
    public float sinkSpeed = 2.5f;
    public int scoreValue = 10;
    public AudioClip deathClip;
    private Action _onDied;


    Animator anim;
    AudioSource enemyAudio;
    ParticleSystem hitParticles;
    CapsuleCollider capsuleCollider;
    bool isDead;
    bool isSinking;
    public bool BenchmarkImmortal { get; set; }
    EnemySpawner owner;
    AudioClip originalClip;
    Vector3 originalParticlePosition;

    public void ResetForSpawn(bool benchmark, EnemySpawner spawner, Action onDied)
    {
        using (new EnemyProfilerSample("Enemy.Health.ResetForSpawn"))
        {
            CancelInvoke();
            StopAllCoroutines();
            currentHealth = startingHealth;
            isDead = isSinking = false;
            _onDied = onDied;
            BenchmarkImmortal = benchmark;
            owner = spawner;
            using (new EnemyProfilerSample("Enemy.Physics.ResetCollider"))
                capsuleCollider.isTrigger = false;
            using (new EnemyProfilerSample("Enemy.Effects.Reset"))
            {
                enemyAudio.Stop();
                enemyAudio.clip = originalClip;
                hitParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                hitParticles.transform.localPosition = originalParticlePosition;
            }
            using (new EnemyProfilerSample("Enemy.Animator.Reset"))
            {
                using (new EnemyProfilerSample("Enemy.Animator.Rebind"))
                    anim.Rebind();
                using (new EnemyProfilerSample("Enemy.Animator.UpdateZero"))
                    anim.Update(0f);
            }
        }
    }


    void Awake()
    {
        //Menapatkan reference komponen
        anim = GetComponent<Animator>();
        enemyAudio = GetComponent<AudioSource>();
        originalClip = enemyAudio.clip;
        hitParticles = GetComponentInChildren<ParticleSystem>();
        originalParticlePosition = hitParticles.transform.localPosition;
        capsuleCollider = GetComponent<CapsuleCollider>();

        //Set current health
        currentHealth = startingHealth;
    }


    void Update()
    {
        //Check jika sinking
        if (isSinking)
        {
            //memindahkan object kebawah
            transform.Translate(-Vector3.up * sinkSpeed * Time.deltaTime);
        }
    }

    public void SetDeathAction(Action onEnemyDied)
    {
        _onDied = onEnemyDied;
    }

    public void BeginBenchmarkDeath()
    {
        if (isDead) return;
        using (new EnemyProfilerSample("Enemy.Health.BeginBenchmarkDeath"))
        {
            currentHealth = 0;
            using (new EnemyProfilerSample("Enemy.Navigation.StopForDeath"))
            {
                var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent.isOnNavMesh) agent.ResetPath();
                agent.enabled = false;
            }
            using (new EnemyProfilerSample("Enemy.Physics.FreezeForDeath"))
                GetComponent<Rigidbody>().isKinematic = true;
            Death();
        }
    }

    public void TakeDamage(int amount, Vector3 hitPoint)
    {
        //Check jika dead
        if (isDead)
            return;

        //play audio
        enemyAudio.Play();

        //kurangi health
        if (!BenchmarkImmortal) currentHealth -= amount;

        //Ganti posisi particle
        hitParticles.transform.position = hitPoint;

        //Play particle system
        hitParticles.Play();

        //Dead jika health <= 0
        if (currentHealth <= 0)
        {
            Death();
        }
    }


    void Death()
    {
        //set isdead
        isDead = true;

        //SetCapcollider ke trigger
        using (new EnemyProfilerSample("Enemy.Physics.DeathCollider"))
            capsuleCollider.isTrigger = true;

        //trigger play animation Dead
        anim.SetTrigger("Dead");

        //Play Sound Dead
        enemyAudio.clip = deathClip;
        enemyAudio.Play();
        _onDied?.Invoke();
    }

    public void StartSinking()
    {
        if (BenchmarkImmortal || isSinking || !isDead) return;
        //disable Navmesh Component
        GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
        //Set rigisbody ke kimematic
        GetComponent<Rigidbody>().isKinematic = true;
        isSinking = true;
        ScoreManager.score += scoreValue;
        Invoke("FinishDespawn", 2f);
    }
    void FinishDespawn()
    {
        if (owner != null) owner.Despawn(this);
        else Destroy(gameObject);
    }
}
