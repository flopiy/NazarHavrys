using System.Collections.Generic;
using UnityEngine;

namespace FungalCurse.Systems
{
    /// <summary>
    /// "Fungal Curse: Echoes of the Mire" — Object Pooler.
    ///
    /// Architectural framework that replaces Instantiate/Destroy for high-churn objects
    /// (projectiles, spores, hit-sparks). Pooling keeps a fixed set of instances alive and
    /// recycles them, eliminating per-shot allocations and the GC spikes they cause.
    ///
    /// Usage:
    ///   1. Add this component to a bootstrap GameObject in the scene.
    ///   2. Define one <see cref="Pool"/> entry per prefab tag (e.g. "PlayerProjectile").
    ///   3. Spawn:   ObjectPooler.Instance.Spawn("PlayerProjectile", muzzle.position, rotation);
    ///   4. Return:  ObjectPooler.Instance.Despawn(go);   // never Destroy()
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObjectPooler : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Pool
        {
            [Tooltip("Unique key used when spawning from this pool.")]
            public string tag;
            public GameObject prefab;
            [Tooltip("Instances created up-front during Awake (prewarm).")]
            public int initialSize = 16;
            [Tooltip("If true, the pool allocates extra instances on demand when empty. " +
                     "If false, the oldest active instance is recycled instead.")]
            public bool expandable = true;
        }

        public static ObjectPooler Instance { get; private set; }

        [SerializeField] private List<Pool> pools = new List<Pool>();

        // Inactive instances ready to be reused, keyed by tag.
        private readonly Dictionary<string, Queue<GameObject>> _available =
            new Dictionary<string, Queue<GameObject>>();
        // Config lookup by tag.
        private readonly Dictionary<string, Pool> _config = new Dictionary<string, Pool>();
        // Reverse lookup so Despawn(go) knows which pool an instance belongs to.
        private readonly Dictionary<GameObject, string> _ownership =
            new Dictionary<GameObject, string>();
        // FIFO of active instances per tag, used when a non-expandable pool must recycle.
        private readonly Dictionary<string, Queue<GameObject>> _active =
            new Dictionary<string, Queue<GameObject>>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            foreach (Pool pool in pools)
                RegisterPool(pool);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------------------------------------------------------------------
        // Pool registration & growth
        // ---------------------------------------------------------------------
        /// <summary>
        /// Dynamic, runtime-safe registration hook. If the pool is already registered,
        /// does nothing. Otherwise registers and prewarms the pool immediately.
        /// </summary>
        public void EnsurePoolRegistered(string tag, GameObject prefab, int initialSize)
        {
            if (string.IsNullOrEmpty(tag) || prefab == null) return;
            if (_config.ContainsKey(tag)) return;

            Pool pool = new Pool { tag = tag, prefab = prefab, initialSize = initialSize, expandable = true };
            RegisterPool(pool);
        }

        private void RegisterPool(Pool pool)
        {
            if (string.IsNullOrEmpty(pool.tag) || pool.prefab == null)
            {
                Debug.LogWarning("[ObjectPooler] Skipping a pool with an empty tag or null prefab.", this);
                return;
            }
            if (_config.ContainsKey(pool.tag))
            {
                Debug.LogWarning($"[ObjectPooler] Duplicate pool tag '{pool.tag}' ignored.", this);
                return;
            }

            _config[pool.tag] = pool;
            _available[pool.tag] = new Queue<GameObject>(pool.initialSize);
            _active[pool.tag] = new Queue<GameObject>(pool.initialSize);

            for (int i = 0; i < pool.initialSize; i++)
                CreateInstance(pool.tag);
        }

        private GameObject CreateInstance(string tag)
        {
            Pool pool = _config[tag];
            GameObject go = Instantiate(pool.prefab, transform); // only ever at prewarm / growth
            go.name = $"{pool.prefab.name}_{tag}";
            go.SetActive(false);
            _ownership[go] = tag;
            _available[tag].Enqueue(go);
            return go;
        }

        // ---------------------------------------------------------------------
        // Spawn
        // ---------------------------------------------------------------------
        public GameObject Spawn(string tag, Vector3 position, Quaternion rotation)
        {
            if (!_config.ContainsKey(tag))
            {
                Debug.LogError($"[ObjectPooler] No pool registered with tag '{tag}'.", this);
                return null;
            }

            GameObject go = AcquireInstance(tag);
            if (go == null) return null;

            Transform t = go.transform;
            t.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
            _active[tag].Enqueue(go);

            // Fire the spawn lifecycle hook on every pooled component that wants it.
            var components = go.GetComponentsInChildren<IPooledObject>(true);
            for (int i = 0; i < components.Length; i++)
                components[i].OnObjectSpawn();

            return go;
        }

        /// <summary>Generic convenience: spawn and fetch a component in one call.</summary>
        public T Spawn<T>(string tag, Vector3 position, Quaternion rotation) where T : Component
        {
            GameObject go = Spawn(tag, position, rotation);
            return go != null ? go.GetComponent<T>() : null;
        }

        private GameObject AcquireInstance(string tag)
        {
            Queue<GameObject> queue = _available[tag];

            if (queue.Count > 0)
                return queue.Dequeue();

            // Empty pool: grow, or recycle the oldest active instance.
            if (_config[tag].expandable)
            {
                GameObject created = CreateInstance(tag);
                return _available[tag].Dequeue() == created ? created : created;
            }

            if (_active[tag].Count > 0)
            {
                GameObject recycled = _active[tag].Dequeue();
                Despawn(recycled);          // returns it to 'available'
                return _available[tag].Dequeue();
            }

            Debug.LogWarning($"[ObjectPooler] Pool '{tag}' exhausted and not expandable.", this);
            return null;
        }

        // ---------------------------------------------------------------------
        // Despawn (return to pool) — replaces Destroy()
        // ---------------------------------------------------------------------
        public void Despawn(GameObject go)
        {
            if (go == null) return;

            if (!_ownership.TryGetValue(go, out string tag))
            {
                // Not a pooled object — fall back to Destroy to avoid leaking foreign objects.
                Debug.LogWarning($"[ObjectPooler] '{go.name}' is not pool-owned; destroying instead.", this);
                Destroy(go);
                return;
            }

            if (!go.activeSelf) return; // already pooled

            var components = go.GetComponentsInChildren<IPooledObject>(true);
            for (int i = 0; i < components.Length; i++)
                components[i].OnObjectDespawn();

            go.SetActive(false);
            go.transform.SetParent(transform, false);
            _available[tag].Enqueue(go);
        }

        /// <summary>Despawn after a delay without coroutines on the caller.</summary>
        public void Despawn(GameObject go, float delay)
        {
            if (go == null) return;
            StartCoroutine(DespawnAfter(go, delay));
        }

        private System.Collections.IEnumerator DespawnAfter(GameObject go, float delay)
        {
            yield return new WaitForSeconds(delay);
            Despawn(go);
        }
    }
}
