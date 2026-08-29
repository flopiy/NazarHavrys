using UnityEngine;

namespace FungalCurse.Combat
{
    /// <summary>
    /// "Thornfall" — a single magical spike summoned high above the battlefield that plummets
    /// straight down as part of the wizard's area-of-effect Spike Storm ability.
    ///
    /// Behaviour:
    ///   * Falls at a constant downward velocity (kinematic, gravity-independent so the timing is
    ///     deterministic regardless of the project's global gravity).
    ///   * Deals damage ONCE to every enemy collider it passes through (it pierces enemies, so a
    ///     column of stacked enemies all get hit by the same spike).
    ///   * Embeds and fades shortly after striking solid ground, or self-destructs after its lifetime
    ///     so nothing ever lingers in the scene.
    ///
    /// The caster (<c>WizardStatsAndCombat</c>) configures damage and the collision masks at spawn
    /// time via <see cref="Configure"/>, so this prefab stays scene-agnostic and pool-free.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public sealed class FallingSpike : MonoBehaviour
    {
        [Header("Fall")]
        [Tooltip("Constant downward speed (units/second).")]
        [SerializeField] private float fallSpeed = 20f;
        [Tooltip("Hard cap before the spike removes itself even if it never hits ground.")]
        [SerializeField] private float maxLifeTime = 3f;

        [Header("Impact")]
        [SerializeField] private int damage = 10;
        [Tooltip("Layers that take damage from the spike (enemies).")]
        [SerializeField] private LayerMask targetMask = 0;
        [Tooltip("Solid layers that stop the spike (ground / tilemap).")]
        [SerializeField] private LayerMask groundMask = 0;
        [Tooltip("Seconds the embedded spike stays visible after striking ground before it fades out.")]
        [SerializeField] private float embedTime = 0.4f;

        private Rigidbody2D _rb;
        private Collider2D _col;
        private SpriteRenderer _sr;
        private float _timer;
        private bool _impacted;

        /// <summary>Injected by the caster so the spike carries the right power and masks.</summary>
        public void Configure(int spikeDamage, LayerMask enemies, LayerMask ground)
        {
            damage = spikeDamage;
            targetMask = enemies;
            groundMask = ground;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<Collider2D>();
            _sr = GetComponentInChildren<SpriteRenderer>();
            _rb.gravityScale = 0f;
            _col.isTrigger = true;
        }

        private void OnEnable()
        {
            _timer = 0f;
            _impacted = false;
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            _rb.linearVelocity = Vector2.down * fallSpeed;
        }

        private void Update()
        {
            if (_impacted) return;
            _timer += Time.deltaTime;
            if (_timer >= maxLifeTime)
                Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            int layerBit = 1 << other.gameObject.layer;

            // Damage any enemy in the fall path. The spike pierces enemies (it is only stopped by ground),
            // so each distinct enemy collider is hit exactly once via OnTriggerEnter2D.
            if ((targetMask.value & layerBit) != 0)
            {
                if (other.TryGetComponent(out IDamageable damageable))
                    damageable.TakeDamage(damage);
            }

            // Solid ground stops the spike and starts the embed/fade.
            if ((groundMask.value & layerBit) != 0)
                Impact();
        }

        private void Impact()
        {
            if (_impacted) return;
            _impacted = true;
            _rb.linearVelocity = Vector2.zero;
            _col.enabled = false;
            Destroy(gameObject, embedTime);
        }
    }
}
