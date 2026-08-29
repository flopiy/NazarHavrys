using System;
using UnityEngine;
using FungalCurse.Combat;

namespace FungalCurse.Enemies
{
    /// <summary>
    /// "Fungal Curse: Echoes of the Mire" — modular combat unit with an optional boss scaler.
    ///
    /// SCOPE (single responsibility): this component owns ONLY combat stats (health/damage), the
    /// boss-scaling pass, and death resolution. It exposes a static <see cref="OnScoreAwarded"/> hook
    /// so score/UI systems can subscribe WITHOUT this class ever referencing them directly.
    ///
    /// Boss rule (evaluated once at runtime init):
    ///   When <see cref="isBoss"/> is true, baseHealth ×5, baseDamage ×3, the Transform scale is
    ///   multiplied uniformly ×2, and exactly 500 bonus points are awarded on destruction.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]   // scene safety: physics body for an active combatant
    [RequireComponent(typeof(Collider2D))]    // so projectiles / hits can register on it
    [DisallowMultipleComponent]
    public sealed class EliteEnemyAI : MonoBehaviour, IDamageable
    {
        /// <summary>
        /// Decoupled score channel. A score/UI manager subscribes; this enemy just invokes it with the
        /// points value on death. Keeps combat and UI registration in separate scripts.
        /// </summary>
        public static event Action<int> OnScoreAwarded;

        [Header("Boss Toggle")]
        [Tooltip("Evaluated once in Awake. When true the unit is scaled into a boss.")]
        [SerializeField] private bool isBoss = false;

        [Tooltip("Evaluated once in Awake. When true the unit is scaled into a mini-boss.")]
        [SerializeField] private bool isMiniBoss = false;

        [Header("Base Combat Stats")]
        [SerializeField] private int baseHealth = 20;
        [SerializeField] private int baseDamage = 4;

        [Header("Scoring")]
        [Tooltip("Points awarded for a normal (non-boss/non-miniboss) kill.")]
        [SerializeField] private int normalKillPoints = 35;
        [Tooltip("Bonus points awarded for a mini-boss kill.")]
        [SerializeField] private int miniBossBonusPoints = 120;
        [Tooltip("Bonus points awarded for a boss kill (spec: exactly 500).")]
        [SerializeField] private int bossBonusPoints = 500;

        [Header("Boss Scaler Multipliers")]
        [SerializeField] private int bossHealthMultiplier = 5;
        [SerializeField] private int bossDamageMultiplier = 3;
        [SerializeField] private float bossScaleMultiplier = 2f;

        [Header("Mini-Boss Scaler Multipliers")]
        [SerializeField] private float miniBossHealthMultiplier = 2.5f;
        [SerializeField] private float miniBossDamageMultiplier = 2f;
        [SerializeField] private float miniBossScaleMultiplier = 1.4f;

        // Runtime-resolved combat values (after the optional boss pass).
        private int _currentHealth;
        private int _currentDamage;
        private bool _isDead;
        private float _contactTimer;

        [Header("Contact Damage Configuration")]
        [Tooltip("Seconds between successive contact damage ticks on the player.")]
        [SerializeField] private float contactDamageCooldown = 0.5f;

        // Public read-only surface for other combat systems.
        public bool IsBoss => isBoss;
        public bool IsMiniBoss => isMiniBoss;
        public int CurrentHealth => _currentHealth;
        public int MaxHealth => isBoss ? baseHealth * bossHealthMultiplier : (isMiniBoss ? Mathf.RoundToInt(baseHealth * miniBossHealthMultiplier) : baseHealth);
        public int CurrentDamage => _currentDamage; // contact/attack damage other systems can read

        private void Awake()
        {
            // Single, runtime-initialisation scaler pass.
            if (isBoss)
            {
                _currentHealth = baseHealth * bossHealthMultiplier; // ×5 HP
                _currentDamage = baseDamage * bossDamageMultiplier; // ×3 damage
                transform.localScale *= bossScaleMultiplier;        // ×2 uniform Transform scale
            }
            else if (isMiniBoss)
            {
                _currentHealth = Mathf.RoundToInt(baseHealth * miniBossHealthMultiplier); // ×2.5 HP
                _currentDamage = Mathf.RoundToInt(baseDamage * miniBossDamageMultiplier); // ×2 damage
                transform.localScale *= miniBossScaleMultiplier;                          // ×1.4 uniform scale
            }
            else
            {
                _currentHealth = baseHealth;
                _currentDamage = baseDamage;
            }
        }

        private void Update()
        {
            if (_contactTimer > 0f) _contactTimer -= Time.deltaTime;
        }

        private void OnCollisionEnter2D(Collision2D collision) => HandleContactDamage(collision.gameObject);
        private void OnCollisionStay2D(Collision2D collision) => HandleContactDamage(collision.gameObject);

        private void HandleContactDamage(GameObject targetGO)
        {
            if (_isDead || _contactTimer > 0f) return;

            // Check if the touched object is the Player and has an IDamageable stats component
            if (targetGO.CompareTag("Player"))
            {
                var damageable = targetGO.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(_currentDamage);
                    _contactTimer = contactDamageCooldown;
                    Debug.Log($"[EliteEnemyAI] {gameObject.name} dealt {_currentDamage} contact damage to Player.");
                }
            }
        }

        /// <summary>Modular damage entry point (IDamageable). Routes all incoming damage through here.</summary>
        public void TakeDamage(int amount)
        {
            if (_isDead || amount <= 0) return;

            _currentHealth = Mathf.Max(0, _currentHealth - amount);

            // Visual feedback: Flash white and halt momentarily
            StartCoroutine(FlashHurt());

            if (_currentHealth == 0)
                Die();
        }

        private System.Collections.IEnumerator FlashHurt()
        {
            // Flash white
            var renderers = GetComponentsInChildren<SpriteRenderer>();
            foreach (var r in renderers) r.color = Color.red; // Use red tint for hurt
            
            // Halt momentum
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
            {
                Vector2 prevVel = rb.linearVelocity;
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.1f);
                if (!_isDead) rb.linearVelocity = prevVel * 0.5f;
            }
            else
            {
                yield return new WaitForSeconds(0.1f);
            }

            foreach (var r in renderers) r.color = Color.white;
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;

            // Award score through the decoupled event: 500 for a boss, 120 for a mini-boss, 35 for normal.
            int award = isBoss ? bossBonusPoints : (isMiniBoss ? miniBossBonusPoints : normalKillPoints);
            if (award != 0)
                OnScoreAwarded?.Invoke(award);

            // Unity 6 2D Animation (com.unity.2d.animation) DeformationSystem safety:
            // Destroying a GameObject while its SpriteSkin components are still ACTIVE corrupts the
            // shared deformation buffer, which makes other skeletal rigs in the scene (including the
            // Player) become disjointed and "fall apart" — most visibly in standalone builds.
            //
            // The robust, build-safe fix is to let each SpriteSkin unregister itself cleanly through
            // its normal OnDisable pass first: disable the skins, deactivate the whole GameObject
            // (which flushes the deformation system in one clean step), and only THEN destroy it.
            var skins = GetComponentsInChildren<UnityEngine.U2D.Animation.SpriteSkin>(true);
            foreach (var skin in skins)
            {
                if (skin != null) skin.enabled = false;
            }

            // Stop taking part in gameplay immediately so nothing interacts with a dying enemy.
            foreach (var col in GetComponentsInChildren<Collider2D>(true))
                col.enabled = false;

            // Deactivating triggers a clean OnDisable on every SpriteSkin at once, then the now
            // inactive object is destroyed a frame later — never destroyed while skins are active.
            gameObject.SetActive(false);
            Destroy(gameObject, 0.02f);
        }
    }

}

