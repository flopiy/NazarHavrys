using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using FungalCurse.Combat;

namespace FungalCurse.Player
{
    /// <summary>
    /// "Fungal Curse: Echoes of the Mire" — the wizard's stat-monitoring matrix and self-contained
    /// ability handler.
    ///
    /// SCOPE (single responsibility): this component owns ONLY the three core resources
    /// (Health/Mana/Stamina) and the two resource-gated abilities that spend them (spell cast + blink).
    /// It deliberately contains NO locomotion, NO UI registration, and NO score logic — those live in
    /// their own components.
    ///
    /// INPUT BACKEND NOTE:
    ///   This project's Active Input Handling is the Input System package, so the legacy
    ///   <c>UnityEngine.Input.GetButton("Fire1")</c> API would throw at runtime. To honor the requested
    ///   "poll inside Update" design while staying runtime-safe, we poll the New Input System devices
    ///   directly: <c>Mouse.current.leftButton</c> is the Fire1 equivalent, and
    ///   <c>Keyboard.current.leftShiftKey</c> is the blink key.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]   // blink uses MovePosition -> needs a body
    [RequireComponent(typeof(Collider2D))]    // so hazards/projectiles can detect the wizard
    [DisallowMultipleComponent]
    public sealed class WizardStatsAndCombat : MonoBehaviour, IDamageable, IKillable
    {
        // -----------------------------------------------------------------
        // Stat monitoring matrix
        // -----------------------------------------------------------------
        [Header("Health (int)")]
        [Tooltip("Maximum hit points. Current health is initialised to this at Awake.")]
        [SerializeField] private int maxHealth = 100;

        [Header("Mana (float)")]
        [SerializeField] private float maxMana = 100f;
        [Tooltip("Mana regenerated per second (set 0 to disable).")]
        [SerializeField] private float manaRegenPerSecond = 12f;

        [Header("Stamina (float)")]
        [SerializeField] private float maxStamina = 100f;
        [Tooltip("Stamina regenerated per second (set 0 to disable).")]
        [SerializeField] private float staminaRegenPerSecond = 20f;

        // -----------------------------------------------------------------
        // Ability configuration
        // -----------------------------------------------------------------
        [Header("Magic Arrow (Left Mouse / Attack)")]
        [Tooltip("Projectile prefab fired as the basic magic arrow.")]
        [SerializeField] private GameObject magicProjectilePrefab;
        [Tooltip("Spawn origin + facing reference for the arrow.")]
        [SerializeField] private Transform castPoint;
        [Tooltip("Mana subtracted per arrow. Refused if current mana is below this.")]
        [SerializeField] private float arrowManaCost = 6f;
        [SerializeField] private float arrowCooldown = 0.3f;
        [SerializeField] private float arrowSpeed = 16f;
        [SerializeField] private int arrowBaseDamage = 5;

        [Header("Spike Storm — AoE (Q / Right Mouse)")]
        [Tooltip("Spike prefab summoned above the target. If empty, loaded from Resources/FX/FallingSpike.")]
        [SerializeField] private GameObject spikePrefab;
        [Tooltip("Mana subtracted per Spike Storm.")]
        [SerializeField] private float spikeManaCost = 28f;
        [Tooltip("Cooldown of the Spike Storm ability in seconds.")]
        [SerializeField] private float spikeCooldown = 4f;
        [Tooltip("Time delay in seconds between spawning each individual spike in the storm.")]
        [SerializeField] private float spikeSpawnDelay = 0.15f;
        [Tooltip("Radius around the wizard searched for the nearest enemy to target.")]
        [SerializeField] private float spikeSearchRadius = 12f;
        [Tooltip("How many spikes rain down per cast.")]
        [SerializeField] private int spikeCount = 6;
        [Tooltip("Horizontal width the spikes are spread across, centred on the target.")]
        [SerializeField] private float spikeAreaWidth = 4.5f;
        [Tooltip("Height above the target the spikes spawn at.")]
        [SerializeField] private float spikeSpawnHeight = 11f;
        [SerializeField] private int spikeBaseDamage = 9;
        [Tooltip("Layers treated as enemies for targeting and damage.")]
        [SerializeField] private LayerMask enemyMask;

        [Header("Magic Shield (F)")]
        [Tooltip("Shield bubble prefab. If empty, loaded from Resources/FX/MagicShield.")]
        [SerializeField] private GameObject shieldPrefab;
        [Tooltip("Local offset of the shield visual relative to the player's pivot (e.g. Y = 1.1f to center on torso).")]
        [SerializeField] private Vector3 shieldLocalOffset = new Vector3(0f, 1.1f, 0f);
        [Tooltip("Mana subtracted to raise the shield.")]
        [SerializeField] private float shieldManaCost = 30f;
        [Tooltip("Mana drained per second while the shield is active.")]
        [SerializeField] private float shieldManaDrainPerSecond = 25f;
        [Tooltip("Seconds the shield absorbs ALL incoming damage.")]
        [SerializeField] private float shieldDuration = 4f;
        [Tooltip("Cooldown counted from the moment the shield drops.")]
        [SerializeField] private float shieldCooldown = 6f;

        [Header("Blink (Left Shift)")]
        [Tooltip("Distance the body is shifted forward via Rigidbody2D.MovePosition.")]
        [SerializeField] private float blinkDistance = 3.5f;
        [Tooltip("Stamina subtracted per blink. The blink is refused if current stamina is below this.")]
        [SerializeField] private float blinkStaminaCost = 25f;
        [Tooltip("Mana subtracted per blink. The blink is refused if current mana is below this.")]
        [SerializeField] private float blinkManaCost = 15f;
        [Tooltip("Solid Tilemap / ground layers the forward barrier ray filters against.")]
        [SerializeField] private LayerMask groundLayer;
        [Tooltip("Body half-width kept clear of a wall so we never land inside a solid collider.")]
        [SerializeField] private float blinkBodyClearance = 0.4f;

        [Header("Facing Reference")]
        [Tooltip("Visual root whose localScale.x sign defines facing (matches the project flip rig). " +
                 "If empty, this transform is used.")]
        [SerializeField] private Transform visualRoot;

        [Header("Out of Bounds")]
        [Tooltip("Y coordinate threshold below which the player is considered fallen out of bounds and dies.")]
        [SerializeField] private float fallLimitY = -11f;

        [Header("Input Asset (New)")]
        [SerializeField] private InputActionAsset inputActions;

        // -----------------------------------------------------------------
        // Runtime state (custom internal variables)
        // -----------------------------------------------------------------
        private Animator _animator;

        private InputActionMap _playerMap;
        private InputAction _attackAction, _blinkAction, _spikeAction, _shieldAction;

        // Ability cooldowns / shield runtime state
        private float _arrowCooldownTimer;
        private float _spikeCooldownTimer;
        private float _shieldCooldownTimer;
        private float _shieldTimer;
        private GameObject _shieldVisual;

        /// <summary>True while the Magic Shield is up and absorbing damage.</summary>
        public bool IsShieldActive => _shieldTimer > 0f;

        private int _currentHealth;     // live HP, clamped [0, maxHealth]
        private float _currentMana;     // live mana, clamped [0, maxMana]
        private float _currentStamina;  // live stamina, clamped [0, maxStamina]
        private Rigidbody2D _rb;         // cached in Awake — never fetched in the loop
        private bool _isDead;

        private int _currentLevel = 1;
        private int _currentXP = 0;
        [SerializeField] private int xpToNextLevel = 100;

        // Read-only accessors for any external HUD (kept out of this script per single-responsibility).
        public int CurrentHealth => _currentHealth;
        public int MaxHealth => maxHealth;
        public float CurrentMana => _currentMana;
        public float MaxMana => maxMana;
        public float CurrentStamina => _currentStamina;
        public float MaxStamina => maxStamina;
        public bool IsDead => _isDead;

        public int CurrentLevel => _currentLevel;
        public int CurrentXP => _currentXP;
        public int XPToNextLevel => xpToNextLevel;

        // Ability accessors for HUD buttons
        public float ArrowCooldownTimer => _arrowCooldownTimer;
        public float ArrowCooldownMax => arrowCooldown;
        public float ArrowCooldownNormalized => arrowCooldown > 0f ? Mathf.Clamp01(_arrowCooldownTimer / arrowCooldown) : 0f;
        public float ArrowManaCost => arrowManaCost;

        public float SpikeCooldownTimer => _spikeCooldownTimer;
        public float SpikeCooldownMax => spikeCooldown;
        public float SpikeCooldownNormalized => spikeCooldown > 0f ? Mathf.Clamp01(_spikeCooldownTimer / spikeCooldown) : 0f;
        public float SpikeManaCost => spikeManaCost;

        public float ShieldCooldownTimer => _shieldCooldownTimer;
        public float ShieldCooldownMax => shieldDuration + shieldCooldown;
        public float ShieldCooldownNormalized => (shieldDuration + shieldCooldown) > 0f ? Mathf.Clamp01(_shieldCooldownTimer / (shieldDuration + shieldCooldown)) : 0f;
        public float ShieldManaCost => shieldManaCost;

        public float BlinkManaCost => blinkManaCost;
        public float BlinkStaminaCost => blinkStaminaCost;

        public void CastArrow() => TryCastArrow();
        public void CastSpikeStorm() => TryCastSpikeStorm();
        public void RaiseShield() => TryRaiseShield();
        public void Blink() => TryBlink();

        private bool _attackRequested;

        private void OnEnable()
        {
            // Subscribe to static score-awarded event as experience points (XP)
            FungalCurse.Enemies.EliteEnemyAI.OnScoreAwarded += GainXP;

            if (inputActions != null)
            {
                _playerMap = inputActions.FindActionMap("Player", true);
                _attackAction = _playerMap.FindAction("Attack", true);
                _blinkAction = _playerMap.FindAction("Dash", true);
                _spikeAction = _playerMap.FindAction("Spikes", false);
                _shieldAction = _playerMap.FindAction("Shield", false);

                _attackAction.performed += OnAttackPerformed;
                // Use robust direct Keyboard polling for Left Ctrl to guarantee 100% reliable teleportation
                // _blinkAction.performed += OnBlinkPerformed;
                if (_spikeAction != null) _spikeAction.performed += OnSpikesPerformed;
                if (_shieldAction != null) _shieldAction.performed += OnShieldPerformed;

                _playerMap.Enable();
            }
        }

        private void OnDisable()
        {
            FungalCurse.Enemies.EliteEnemyAI.OnScoreAwarded -= GainXP;

            if (_playerMap != null)
            {
                _attackAction.performed -= OnAttackPerformed;
                // _blinkAction.performed -= OnBlinkPerformed;
                if (_spikeAction != null) _spikeAction.performed -= OnSpikesPerformed;
                if (_shieldAction != null) _shieldAction.performed -= OnShieldPerformed;
                _playerMap.Disable();
            }
        }

        private void OnAttackPerformed(InputAction.CallbackContext context)
        {
            if (_isDead) return;
            _attackRequested = true;
        }

        private void OnBlinkPerformed(InputAction.CallbackContext context)
        {
            if (_isDead) return;
            Debug.Log("[WizardStatsAndCombat] OnBlinkPerformed called!");
            TryBlink();
        }

        private void OnSpikesPerformed(InputAction.CallbackContext context)
        {
            if (_isDead) return;
            TryCastSpikeStorm();
        }

        private void OnShieldPerformed(InputAction.CallbackContext context)
        {
            if (_isDead) return;
            TryRaiseShield();
        }

        private void GainXP(int amount)
        {
            if (_isDead) return;
            _currentXP += amount;
            Debug.Log($"[WizardStatsAndCombat] Gained {amount} XP. Progress: {_currentXP}/{xpToNextLevel}");

            // Permanent Upgrading Points saved into PlayerPrefs for Main Menu meta-progression
            int currentPoints = PlayerPrefs.GetInt("UpgradePoints", 0);
            PlayerPrefs.SetInt("UpgradePoints", currentPoints + amount);
            PlayerPrefs.Save();
            Debug.Log($"[WizardStatsAndCombat] Saved UpgradePoints to PlayerPrefs: {currentPoints + amount}");

            while (_currentXP >= xpToNextLevel)
            {
                LevelUp();
            }
        }

        private void LevelUp()
        {
            _currentXP -= xpToNextLevel;
            _currentLevel++;
            xpToNextLevel = Mathf.RoundToInt(xpToNextLevel * 1.5f);

            // Level up rewards: scale player max stats and fully restore resources
            maxHealth += 10;
            _currentHealth = maxHealth;
            maxMana += 15f;
            _currentMana = maxMana;
            maxStamina += 15f;
            _currentStamina = maxStamina;

            Debug.Log($"[WizardStatsAndCombat] LEVEL UP! Reached Level {_currentLevel}. Next Level XP required: {xpToNextLevel}");
        }

        private void Awake()
        {
            // Cache the body once (perf constraint: no GetComponent inside Update/FixedUpdate).
            _rb = GetComponent<Rigidbody2D>();
            _animator = GetComponentInChildren<Animator>();

            // Load meta-progression upgrades from PlayerPrefs
            int manaUpgradeLvl = PlayerPrefs.GetInt("Upgrade_MaxMana_Level", 0);
            maxMana += manaUpgradeLvl * 10f; // Minimal, tactical +10 Max Mana per level upgrade

            int blinkUpgradeLvl = PlayerPrefs.GetInt("Upgrade_Blink_Level", 0);
            blinkDistance += blinkUpgradeLvl * 0.3f; // Minimal, tactical +0.3f Blink distance per level upgrade

            // Initialise the stat matrix to full.
            _currentHealth = maxHealth;
            _currentMana = maxMana;
            _currentStamina = maxStamina;

            if (visualRoot == null) visualRoot = transform;

            // Resolve ability prefabs from Resources so the wizard works in every scene without
            // needing per-scene inspector wiring (the player object is duplicated across levels).
            if (spikePrefab == null)
                spikePrefab = Resources.Load<GameObject>("FX/FallingSpike");
            if (shieldPrefab == null)
                shieldPrefab = Resources.Load<GameObject>("FX/MagicShield");

            // Fallback enemy mask if none was assigned in the inspector.
            if (enemyMask == 0)
            {
                int enemiesLayer = LayerMask.NameToLayer("Enemies");
                if (enemiesLayer >= 0) enemyMask = 1 << enemiesLayer;
            }

            BuildShieldVisual();
        }

        // Instantiate the shield bubble once, parented to the root, and keep it hidden until raised.
        private void BuildShieldVisual()
        {
            if (shieldPrefab == null) return;
            _shieldVisual = Instantiate(shieldPrefab, transform.position, Quaternion.identity, transform);
            _shieldVisual.transform.localPosition = shieldLocalOffset;
            _shieldVisual.SetActive(false);
        }

        private void Update()
        {
            if (_isDead) return;

            // Instantly kill player if they fall below platforms (out of bounds)
            if (transform.position.y < fallLimitY)
            {
                Kill();
                return;
            }

            // Direct keyboard polling for Left Ctrl to trigger teleport reliably
            if (Keyboard.current != null && Keyboard.current.leftCtrlKey.wasPressedThisFrame)
            {
                TryBlink();
            }

            if (_attackRequested)
            {
                _attackRequested = false;
                if (UnityEngine.EventSystems.EventSystem.current == null || 
                    !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    TryCastArrow();
                }
            }

            TickTimers();

            // Check for fainting due to complete mana depletion BEFORE we regenerate resources
            if (_currentMana <= 0.001f)
            {
                _currentMana = 0f;
                Faint();
                return;
            }

            RegenerateResources();
        }

        // Counts down every ability cooldown and the active-shield window.
        private void TickTimers()
        {
            float dt = Time.deltaTime;
            if (_arrowCooldownTimer > 0f) _arrowCooldownTimer -= dt;
            if (_spikeCooldownTimer > 0f) _spikeCooldownTimer -= dt;
            if (_shieldCooldownTimer > 0f) _shieldCooldownTimer -= dt;

            if (_shieldTimer > 0f)
            {
                _shieldTimer -= dt;

                // Drain mana quickly while shield is active
                float manaDrain = shieldManaDrainPerSecond * dt;
                _currentMana = Mathf.Max(0f, _currentMana - manaDrain);

                if (_currentMana <= 0f)
                {
                    _shieldTimer = 0f;
                    Debug.Log("[WizardStatsAndCombat] Magic Shield deactivated because mana ran out! Character is now vulnerable.");
                }

                if (_shieldTimer <= 0f)
                {
                    _shieldTimer = 0f;
                    if (_shieldVisual != null) _shieldVisual.SetActive(false);
                    Debug.Log("[WizardStatsAndCombat] Magic Shield faded.");
                }
            }
        }

        // Smoothly refill the float resources over time.
        private void RegenerateResources()
        {
            // Do not regenerate mana while shield is actively drawing power
            if (!IsShieldActive)
            {
                _currentMana = Mathf.Min(maxMana, _currentMana + manaRegenPerSecond * Time.deltaTime);
            }
            _currentStamina = Mathf.Min(maxStamina, _currentStamina + staminaRegenPerSecond * Time.deltaTime);
        }

        // -----------------------------------------------------------------
        // Ability 1: Magic Arrow — basic ranged projectile (Left Mouse / Attack)
        // -----------------------------------------------------------------
        private void TryCastArrow()
        {
            if (magicProjectilePrefab == null || castPoint == null) return;
            if (_arrowCooldownTimer > 0f) return;
            if (_currentMana < arrowManaCost) return;

            _currentMana -= arrowManaCost;
            _arrowCooldownTimer = arrowCooldown;

            if (_animator != null) _animator.SetTrigger("attack");

            int dmgUpgradeLvl = PlayerPrefs.GetInt("Upgrade_AttackDamage_Level", 0);
            int finalDamage = arrowBaseDamage + (dmgUpgradeLvl * 1); // Minimal +1 damage per level upgrade
            Color color = new Color(0.85f, 0.55f, 1f); // arcane violet glow
            Vector3 scale = new Vector3(1.2f, 1.2f, 1f);

            // Orient the arrow to the wizard's facing (project convention: 180° Y-flip when facing left).
            float boltFacing = FacingSign();
            Quaternion rotation = boltFacing >= 0f ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);

            GameObject proj = Instantiate(magicProjectilePrefab, castPoint.position, rotation);
            var magicProj = proj.GetComponent<MagicProjectile>();
            if (magicProj != null)
            {
                LayerMask mask = groundLayer | enemyMask | (1 << LayerMask.NameToLayer("Default"));
                magicProj.InitializeProjectile(arrowSpeed, 2.5f, finalDamage, color, scale, false, false, 1.5f, mask);
            }
            Debug.Log($"[WizardStatsAndCombat] Magic Arrow! Damage: {finalDamage}, Mana: {arrowManaCost}");
        }

        // -----------------------------------------------------------------
        // Ability 2: Spike Storm — AoE spikes rain down on the nearest enemy
        // -----------------------------------------------------------------
        private void TryCastSpikeStorm()
        {
            if (spikePrefab == null) return;
            if (_spikeCooldownTimer > 0f) return;
            if (_currentMana < spikeManaCost) return;

            // Target the nearest enemy; if none is around, drop the storm just ahead of the wizard.
            Vector2 center;
            Transform target = FindNearestEnemy(spikeSearchRadius);
            if (target != null)
                center = target.position;
            else
                center = (Vector2)transform.position + Vector2.right * FacingSign() * 3f;

            _currentMana -= spikeManaCost;
            _spikeCooldownTimer = spikeCooldown;

            if (_animator != null) _animator.SetTrigger("attack");

            // Q + Number (1-9) quantity selection check at runtime using Keyboard state
            int customCount = spikeCount;
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb[UnityEngine.InputSystem.Key.Digit1].isPressed) customCount = 1;
                else if (kb[UnityEngine.InputSystem.Key.Digit2].isPressed) customCount = 2;
                else if (kb[UnityEngine.InputSystem.Key.Digit3].isPressed) customCount = 3;
                else if (kb[UnityEngine.InputSystem.Key.Digit4].isPressed) customCount = 4;
                else if (kb[UnityEngine.InputSystem.Key.Digit5].isPressed) customCount = 5;
                else if (kb[UnityEngine.InputSystem.Key.Digit6].isPressed) customCount = 6;
                else if (kb[UnityEngine.InputSystem.Key.Digit7].isPressed) customCount = 7;
                else if (kb[UnityEngine.InputSystem.Key.Digit8].isPressed) customCount = 8;
                else if (kb[UnityEngine.InputSystem.Key.Digit9].isPressed) customCount = 9;
            }

            StartCoroutine(SpikeStormRoutine(center, customCount));
            Debug.Log($"[WizardStatsAndCombat] Spike Storm at {center} with {customCount} spikes! Mana: {spikeManaCost}");
        }

        private System.Collections.IEnumerator SpikeStormRoutine(Vector2 center, int customCount)
        {
            int dmgUpgradeLvl = PlayerPrefs.GetInt("Upgrade_AttackDamage_Level", 0);
            int spikeDamage = spikeBaseDamage + (dmgUpgradeLvl * 1); // Minimal +1 damage per level upgrade
            LayerMask groundMask = groundLayer | (1 << LayerMask.NameToLayer("Default"));

            int count = Mathf.Max(1, customCount);
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : (float)i / (count - 1);
                float x = center.x + Mathf.Lerp(-spikeAreaWidth * 0.5f, spikeAreaWidth * 0.5f, t);
                x += Random.Range(-0.25f, 0.25f);
                Vector3 spawnPos = new Vector3(x, center.y + spikeSpawnHeight + Random.Range(-0.3f, 0.5f), 0f);

                GameObject spikeGo = Instantiate(spikePrefab, spawnPos, Quaternion.identity);
                var spike = spikeGo.GetComponent<FungalCurse.Combat.FallingSpike>();
                if (spike != null) spike.Configure(spikeDamage, enemyMask, groundMask);

                yield return new WaitForSeconds(spikeSpawnDelay);
            }
        }

        // Scans for the closest enemy collider within range that exposes an IDamageable surface.
        private Transform FindNearestEnemy(float radius)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, enemyMask);
            Transform nearest = null;
            float bestSqr = float.MaxValue;
            foreach (var hit in hits)
            {
                if (hit == null) continue;
                if (hit.GetComponentInParent<IDamageable>() == null) continue;
                float sqr = ((Vector2)hit.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = hit.transform;
                }
            }
            return nearest;
        }

        // -----------------------------------------------------------------
        // Ability 3: Magic Shield — absorbs ALL incoming damage for a duration (F)
        // -----------------------------------------------------------------
        private void TryRaiseShield()
        {
            if (_shieldTimer > 0f) return;          // already up
            if (_shieldCooldownTimer > 0f) return;  // recharging
            if (_currentMana < shieldManaCost) return;

            _currentMana -= shieldManaCost;
            _shieldTimer = shieldDuration;
            // Cooldown only finishes recharging some time after the shield drops.
            _shieldCooldownTimer = shieldDuration + shieldCooldown;

            if (_shieldVisual != null) _shieldVisual.SetActive(true);
            Debug.Log($"[WizardStatsAndCombat] Magic Shield raised for {shieldDuration}s! Mana: {shieldManaCost}");
        }

        // -----------------------------------------------------------------
        // Ability: blink (instant spatial shift via MovePosition)
        // -----------------------------------------------------------------
        private void TryBlink()
        {
            Debug.Log($"[WizardStatsAndCombat] TryBlink called. Stamina: {_currentStamina}/{blinkStaminaCost}, Mana: {_currentMana}/{blinkManaCost}");
            if (_currentStamina < blinkStaminaCost) return;
            if (_currentMana < blinkManaCost) return;

            float facing = FacingSign();
            Vector2 direction = new Vector2(facing, 0f);
            
            // Raise the raycast origin slightly (0.5 units up) to prevent hitting the floor tiles 
            // when standing directly on them.
            Vector2 origin = _rb.position + Vector2.up * 0.5f;

            // 2D horizontal barrier check: if a solid tile lies within the blink path on groundLayer,
            // clamp the shift to stop just short of it so we never teleport inside the Tilemap.
            float distance = blinkDistance;
            RaycastHit2D hit = Physics2D.Raycast(origin, direction, blinkDistance, groundLayer);
            
            if (hit.collider != null)
            {
                Debug.Log($"[WizardStatsAndCombat] Blink raycast hit: {hit.collider.name} on layer {LayerMask.LayerToName(hit.collider.gameObject.layer)} at distance {hit.distance}");
            }

            // Ignore very close hits (distance <= 0.1f) because they represent starting overlaps 
            // from standing directly on tiles or boundaries.
            if (hit.collider != null && hit.distance > 0.1f)
                distance = Mathf.Max(0f, hit.distance - blinkBodyClearance);

            Debug.Log($"[WizardStatsAndCombat] Calculated blink distance: {distance} (base blinkDistance is {blinkDistance})");

            if (distance <= 0f) return; // flush against a wall — nothing to do, keep resources

            _currentStamina -= blinkStaminaCost;
            _currentMana -= blinkManaCost;
            _rb.MovePosition(_rb.position + direction * distance);
            Debug.Log($"[WizardStatsAndCombat] Teleport executed! New position should be close to {_rb.position + direction * distance}");
        }

        // Facing derived from controller or visual root orientation.
        private float FacingSign()
        {
            var controller = GetComponent<WizardPlayerController>();
            if (controller != null) return controller.Facing;
            if (visualRoot != null)
            {
                if (visualRoot.localEulerAngles.y > 90f && visualRoot.localEulerAngles.y < 270f)
                    return -1f;
                if (visualRoot.localScale.x < 0f)
                    return -1f;
            }
            return 1f;
        }

        // -----------------------------------------------------------------
        // Damage / death contracts
        // -----------------------------------------------------------------
        public void TakeDamage(int amount)
        {
            if (_isDead || amount <= 0) return;

            // Magic Shield absorbs ALL incoming damage while it is up.
            if (_shieldTimer > 0f)
            {
                Debug.Log($"[WizardStatsAndCombat] Magic Shield absorbed {amount} damage.");
                return;
            }

            _currentHealth = Mathf.Max(0, _currentHealth - amount);
            if (_currentHealth == 0) Die();
        }

        /// <summary>Un-mitigated instant elimination (used by GreenInstantDeath hazard zones).</summary>
        public void Kill()
        {
            if (_isDead) return;
            _currentHealth = 0;
            Die();
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;

            // Stop any horizontal momentum immediately
            if (_rb != null)
            {
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            }

            // Disable player control scripts so they cannot jump, blink, or move
            var controller = GetComponent<WizardPlayerController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            // Play death animation (vendor skeletal rig trigger)
            var anim = GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.SetTrigger("die");
            }

            // Delegate ruleset handling to GameManager, with scene reload fallback
            if (FungalCurse.Systems.GameManager.Instance != null)
            {
                FungalCurse.Systems.GameManager.Instance.OnPlayerDeath();
            }
            else
            {
                StartCoroutine(ReloadSceneAfterDelay(2.2f));
            }
        }

        private void Faint()
        {
            if (_isDead) return;
            _isDead = true;

            Debug.Log("[WizardStatsAndCombat] Player fainted from complete mana exhaustion!");

            // Stop any horizontal momentum immediately
            if (_rb != null)
            {
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            }

            // Disable player control scripts so they cannot jump, blink, or move
            var controller = GetComponent<WizardPlayerController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            // Hide the active shield if raised
            if (_shieldVisual != null)
            {
                _shieldVisual.SetActive(false);
            }

            // Play faint/death animation (using the "die" animator trigger to drop the character)
            var anim = GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.SetTrigger("die");
            }

            // Delegate ruleset handling to GameManager (loss of a life / level restart)
            if (FungalCurse.Systems.GameManager.Instance != null)
            {
                FungalCurse.Systems.GameManager.Instance.OnPlayerDeath();
            }
            else
            {
                StartCoroutine(ReloadSceneAfterDelay(2.2f));
            }
        }

        private System.Collections.IEnumerator ReloadSceneAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.name);
        }

        private void OnDrawGizmosSelected()
        {
            // Blink path preview in the editor.
            float facing = Application.isPlaying ? FacingSign() : 1f;
            Gizmos.color = Color.cyan;
            Vector3 o = transform.position;
            Gizmos.DrawLine(o, o + Vector3.right * facing * blinkDistance);
        }
    }
}
