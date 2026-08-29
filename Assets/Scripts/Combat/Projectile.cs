using UnityEngine;
using FungalCurse.Systems;

namespace FungalCurse.Combat
{
    /// <summary>
    /// "Fungal Curse: Echoes of the Mire" — pooled projectile (spore bolt).
    ///
    /// Reference implementation of the no-Instantiate / no-Destroy contract. The projectile is
    /// spawned via <see cref="ObjectPooler"/> and ALWAYS returns itself through
    /// <see cref="ObjectPooler.Despawn(GameObject)"/> — on lifetime expiry or on impact —
    /// so no allocation or collection happens during gameplay.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public sealed class Projectile : MonoBehaviour, IPooledObject
    {
        [Header("Flight")]
        [SerializeField] private float speed = 16f;
        [Tooltip("Seconds before the projectile auto-returns to the pool.")]
        [SerializeField] private float lifeTime = 3f;

        [Header("Impact")]
        [SerializeField] private int damage = 1;
        [Tooltip("Layers that cause the projectile to despawn on contact (walls + targets).")]
        [SerializeField] private LayerMask collisionMask;

        private Rigidbody2D _rb;
        private float _lifeTimer;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f; // straight-line spore bolt
        }

        // -----------------------------------------------------------------
        // Pool lifecycle
        // -----------------------------------------------------------------
        public void OnObjectSpawn()
        {
            // Reset timers/velocity every reuse. Direction is derived from spawn rotation:
            // the muzzle sets transform.right when calling ObjectPooler.Spawn().
            _lifeTimer = lifeTime;
            _rb.linearVelocity = transform.right * speed;
        }

        public void OnObjectDespawn()
        {
            // Zero out so a recycled instance never carries stale momentum.
            _rb.linearVelocity = Vector2.zero;
        }

        // -----------------------------------------------------------------
        // Per-frame lifetime
        // -----------------------------------------------------------------
        private void Update()
        {
            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0f)
                ReturnToPool();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Ignore layers not in the mask.
            if ((collisionMask.value & (1 << other.gameObject.layer)) == 0)
                return;

            // Apply damage if the target exposes a damageable surface.
            if (other.TryGetComponent(out IDamageable damageable))
                damageable.TakeDamage(damage);

            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (ObjectPooler.Instance != null)
                ObjectPooler.Instance.Despawn(gameObject);
            else
                gameObject.SetActive(false); // graceful fallback if the pooler is gone
        }
    }

    /// <summary>Minimal damage contract so projectiles stay decoupled from concrete enemies.</summary>
    public interface IDamageable
    {
        void TakeDamage(int amount);
    }
}
