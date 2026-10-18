using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyAllocation { InstantiateDestroy, Pool }

public class EnemySpawner : MonoBehaviour
{
    public EnemyFactory factory;
    public EnemyAllocation allocation = EnemyAllocation.Pool;
    public int PoolExpansions { get; private set; }
    sealed class Entry
    {
        public EnemyHealth health;
        public EnemyAttack attack;
        public Rigidbody body;
        public NavMeshAgent agent;
        public int type;
        public bool kinematic;
        public bool active;
    }
    readonly Dictionary<EnemyHealth, Entry> entries = new Dictionary<EnemyHealth, Entry>();
    Stack<Entry>[] pools;
    EnemyAllocation mode;
    bool overrideCulling;
    AnimatorCullingMode animatorCulling;
    bool updateWhenOffscreen, skinnedMotionVectors;

    public void ConfigureBenchmarkCulling(AnimatorCullingMode culling, bool offscreen, bool motionVectors)
    {
        if (pools != null) throw new InvalidOperationException("Configure culling before initializing the spawner.");
        if (!Enum.IsDefined(typeof(AnimatorCullingMode), culling)) throw new ArgumentException("Invalid animator culling mode.");
        overrideCulling = true;
        animatorCulling = culling;
        updateWhenOffscreen = offscreen;
        skinnedMotionVectors = motionVectors;
    }

    public void Initialize(EnemyAllocation requested)
    {
        if (pools != null)
        {
            if (mode != requested) throw new InvalidOperationException("Cannot change allocation while spawner owns objects.");
            return;
        }
        if (factory == null || factory.enemyPrefab == null || factory.enemyPrefab.Length == 0)
            throw new InvalidOperationException("EnemySpawner requires an EnemyFactory with prefabs.");
        if (!Enum.IsDefined(typeof(EnemyAllocation), requested)) throw new ArgumentException("Invalid allocation mode.");
        foreach (GameObject prefab in factory.enemyPrefab)
            if (prefab == null || !prefab.activeSelf || prefab.GetComponent<EnemyHealth>() == null ||
                prefab.GetComponent<EnemyAttack>() == null || prefab.GetComponent<Rigidbody>() == null ||
                prefab.GetComponent<NavMeshAgent>() == null)
                throw new InvalidOperationException("EnemySpawner requires active prefabs with health, attack, Rigidbody and NavMeshAgent.");
        mode = requested;
        pools = new Stack<Entry>[factory.enemyPrefab.Length];
        for (int i = 0; i < pools.Length; i++) pools[i] = new Stack<Entry>();
        PoolExpansions = 0;
    }

    Entry Create(int type, Vector3 position, Quaternion rotation)
    {
        using (new EnemyProfilerSample("Enemy.Spawner.Create"))
        {
            GameObject obj = Instantiate(factory.enemyPrefab[type], position, rotation, transform);
            // Configure instances once, including prewarmed and subsequently created enemies.
            if (overrideCulling)
            {
                foreach (Animator animator in obj.GetComponentsInChildren<Animator>(true))
                    animator.cullingMode = animatorCulling;
                foreach (SkinnedMeshRenderer renderer in obj.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    renderer.updateWhenOffscreen = updateWhenOffscreen;
                    renderer.skinnedMotionVectors = skinnedMotionVectors;
                }
            }
            var entry = new Entry { health = obj.GetComponent<EnemyHealth>(), attack = obj.GetComponent<EnemyAttack>(),
                body = obj.GetComponent<Rigidbody>(), agent = obj.GetComponent<NavMeshAgent>(), type = type };
            if (entry.health == null || entry.attack == null || entry.body == null || entry.agent == null)
            {
                Destroy(obj);
                throw new InvalidOperationException("Enemy prefab is missing required components.");
            }
            entry.kinematic = entry.body.isKinematic;
            entries.Add(entry.health, entry);
            using (new EnemyProfilerSample("Enemy.Object.Deactivate"))
                obj.SetActive(false);
            return entry;
        }
    }

    public void Prewarm(int type, int count, Vector3 position, Quaternion rotation)
    {
        if (pools == null) Initialize(allocation);
        if (type < 0 || type >= pools.Length || count < 0) throw new ArgumentOutOfRangeException("type/count");
        if (mode != EnemyAllocation.Pool) return;
        while (pools[type].Count < count) pools[type].Push(Create(type, position, rotation));
    }

    public EnemyHealth Spawn(int type, Vector3 position, Quaternion rotation, bool benchmark = false, Action onDied = null)
    {
        using (new EnemyProfilerSample("Enemy.Spawner.Spawn"))
        {
            if (pools == null) Initialize(allocation);
            if (type < 0 || type >= pools.Length) throw new ArgumentOutOfRangeException("type");
            Entry entry;
            if (mode == EnemyAllocation.Pool && pools[type].Count > 0) entry = pools[type].Pop();
            else
            {
                if (mode == EnemyAllocation.Pool) PoolExpansions++;
                entry = Create(type, position, rotation);
            }
            entry.active = true;
            try
            {
                using (new EnemyProfilerSample("Enemy.Navigation.Disable"))
                    entry.agent.enabled = false;
                using (new EnemyProfilerSample("Enemy.Physics.ResetBody"))
                {
                    entry.health.transform.SetPositionAndRotation(position, rotation);
                    entry.body.isKinematic = entry.kinematic;
                    entry.body.velocity = Vector3.zero;
                    entry.body.angularVelocity = Vector3.zero;
                }
                using (new EnemyProfilerSample("Enemy.Object.Activate"))
                    entry.health.gameObject.SetActive(true);
                entry.health.ResetForSpawn(benchmark, this, onDied);
                entry.attack.ResetForSpawn();
                using (new EnemyProfilerSample("Enemy.Navigation.ActivateAndWarp"))
                {
                    entry.agent.enabled = true;
                    if (!entry.agent.Warp(position) || !entry.agent.isOnNavMesh)
                        throw new InvalidOperationException("Enemy spawn point is not on NavMesh.");
                    entry.agent.ResetPath();
                    entry.agent.isStopped = false;
                }
                return entry.health;
            }
            catch { Despawn(entry.health); throw; }
        }
    }

    public void Despawn(EnemyHealth health)
    {
        using (new EnemyProfilerSample("Enemy.Spawner.Despawn"))
        {
            Entry entry;
            if (health == null || !entries.TryGetValue(health, out entry) || !entry.active) return;
            entry.active = false;
            health.CancelInvoke();
            health.SetDeathAction(null);
            using (new EnemyProfilerSample("Enemy.Navigation.ResetPath"))
                if (entry.agent.enabled && entry.agent.isOnNavMesh) entry.agent.ResetPath();
            using (new EnemyProfilerSample("Enemy.Object.Deactivate"))
                health.gameObject.SetActive(false);
            if (mode == EnemyAllocation.Pool) pools[entry.type].Push(entry);
            else
            {
                entries.Remove(health);
                using (new EnemyProfilerSample("Enemy.Spawner.DestroyRequest"))
                    Destroy(health.gameObject);
            }
        }
    }

    public void Clear()
    {
        foreach (var pair in entries)
            if (pair.Key != null) { pair.Key.gameObject.SetActive(false); Destroy(pair.Key.gameObject); }
        entries.Clear();
        pools = null;
        overrideCulling = false;
    }
}
