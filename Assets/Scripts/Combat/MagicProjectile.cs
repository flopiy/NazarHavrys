using UnityEngine;
using FungalCurse.Systems;

namespace FungalCurse.Combat
{
    /// <summary>
    /// "The Midnight Bramble" — the wizard's magic bolt.
    ///
    /// Pool-aware (implements <see cref="IPooledObject"/>): when spawned through the ObjectPooler it
    /// recycles instead of allocating. When Instantiated directly (no pooler) it self-destroys after
    /// its lifetime. Direction is taken from transform.right, so the caster only needs to set rotation.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public sealed class MagicProjectile : MonoBehaviour, IPooledObject
    {
        [Header("Flight")]
        [SerializeField] private float speed = 14f;
        [SerializeField] private float lifeTime = 2.5f;

        [Header("Impact")]
        [SerializeField] private int damage = 1;
        [Tooltip("Layers that the bolt collides with (walls + enemies).")]
        [SerializeField] private LayerMask collisionMask = ~0;

        [Header("Special Behaviors")]
        [SerializeField] private bool pierce = false;
        [SerializeField] private bool explodes = false;
        [SerializeField] private float explosionRadius = 1.5f;

        public int Damage
        {
            get => damage;
            set => damage = value;
        }

        public float Speed
        {
            get => speed;
            set => speed = value;
        }

        public float LifeTime
        {
            get => lifeTime;
            set => lifeTime = value;
        }

        public bool Pierce
        {
            get => pierce;
            set => pierce = value;
        }

        public bool Explodes
        {
            get => explodes;
            set => explodes = value;
        }

        public float ExplosionRadius
        {
            get => explosionRadius;
            set => explosionRadius = value;
        }

        public LayerMask CollisionMask
        {
            get => collisionMask;
            set => collisionMask = value;
        }

        private Rigidbody2D _rb;
        private float _timer;
        private bool _pooled; // true when spawned via ObjectPooler

        /// <summary>
        /// Dynamically configures the visual style and physical properties of this magic projectile.
        /// </summary>
        public void InitializeProjectile(
            float customSpeed, 
            float customLifetime, 
            int customDamage, 
            Color customColor, 
            Vector3 customScale, 
            bool customPierce, 
            bool customExplodes, 
            float customExplosionRadius,
            LayerMask customMask)
        {
            speed = customSpeed;
            lifeTime = customLifetime;
            damage = customDamage;
            pierce = customPierce;
            explodes = customExplodes;
            explosionRadius = customExplosionRadius;
            collisionMask = customMask;

            _timer = lifeTime;

            // Apply custom scale
            transform.localScale = customScale;

            // Apply custom color to SpriteRenderer
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = customColor;
            }

            // Apply custom color to Light2D if present
            var light2D = GetComponentInChildren<UnityEngine.Rendering.Universal.Light2D>();
            if (light2D != null)
            {
                light2D.color = customColor;
            }

            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            _rb.linearVelocity = transform.right * speed;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
        }

        // Pooled spawn path
        public void OnObjectSpawn()
        {
            _pooled = true;
            _timer = lifeTime;
            _rb.linearVelocity = transform.right * speed;
        }

        public void OnObjectDespawn() => _rb.linearVelocity = Vector2.zero;

        // Direct-Instantiate path (no pooler): initialise on enable.
        private void OnEnable()
        {
            if (!_pooled)
            {
                _timer = lifeTime;
                if (_rb == null) _rb = GetComponent<Rigidbody2D>();
                _rb.linearVelocity = transform.right * speed;
            }
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f) Dispose();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if ((collisionMask.value & (1 << other.gameObject.layer)) == 0) return;

            if (explodes)
            {
                Explode();
            }
            else
            {
                if (other.TryGetComponent(out IDamageable damageable))
                    damageable.TakeDamage(damage);
            }

            // If we are piercing and hit an enemy, we continue. 
            // However, hitting the Solid Ground/Obstacles should always dispose the projectile.
            bool hitGround = other.gameObject.layer == LayerMask.NameToLayer("Ground") || other.gameObject.layer == LayerMask.NameToLayer("Default");
            if (!pierce || hitGround)
            {
                Dispose();
            }
        }

        private void Explode()
        {
            Collider2D[] targets = Physics2D.OverlapCircleAll(transform.position, explosionRadius, collisionMask);
            foreach (var target in targets)
            {
                if (target.gameObject == gameObject) continue;
                if (target.TryGetComponent(out IDamageable damageable))
                {
                    damageable.TakeDamage(damage);
                }
            }

            // Optional visual effects could be played here. Since we are doing a lightweight implementation,
            // we can trigger a small temporary particle or just complete the deal cleanly.
        }

        private void Dispose()
        {
            if (_pooled && ObjectPooler.Instance != null)
                ObjectPooler.Instance.Despawn(gameObject);
            else
                Destroy(gameObject);
        }
    }
}
