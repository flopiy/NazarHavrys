using UnityEngine;
using UnityEngine.SceneManagement;
using FungalCurse.Systems;

namespace FungalCurse.Environment
{
    /// <summary>
    /// "Fungal Curse: Echoes of the Mire" — the level-exit portal.
    ///
    /// SINGLE RESPONSIBILITY: this component ONLY handles the scene transition gate. It owns no
    /// combat, health, score, or UI logic — it merely *queries* <see cref="MissionManager"/> and,
    /// optionally, *reacts* to its one-shot unlock event to toggle a locked/unlocked visual.
    ///
    /// Detection model:
    ///   * The portal is a STATIC trigger zone. The Player carries the moving Rigidbody2D, so the
    ///     <see cref="OnTriggerEnter2D"/> callback fires when the player overlaps this collider.
    ///   * Identification is done with a LayerMask bit-test (cheap, allocation-free) rather than
    ///     string tag comparison, so any object on the configured 'Player' layer activates it.
    ///
    /// Gate rule (requirement #2):
    ///   The scene only loads if <see cref="MissionManager.IsLevelClear"/> returns true at the moment
    ///   of entry. Otherwise the portal does nothing (and optionally keeps the "locked" visual).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]   // explicit scene-safety: guarantees trigger events fire
    [RequireComponent(typeof(Collider2D))]    // the trigger volume the player walks into
    [DisallowMultipleComponent]
    public sealed class LevelTeleport : MonoBehaviour
    {
        [Header("Scene Transition")]
        [Tooltip("Build-settings scene name to load when a cleared player enters the portal.")]
        [SerializeField] private string nextSceneName = "Level_02";

        [Header("Player Detection")]
        [Tooltip("Layer(s) considered 'the Player'. Set this to your Player layer in the Inspector.")]
        [SerializeField] private LayerMask playerLayer;

        [Header("Portal Visual State (optional)")]
        [Tooltip("Visual shown while the portal is LOCKED (objective not yet complete). Optional.")]
        [SerializeField] private GameObject lockedVisual;

        [Tooltip("Visual shown once the portal is UNLOCKED (objective complete). Optional.")]
        [SerializeField] private GameObject unlockedVisual;

        // Cached components — resolved once in Awake, never in the hot loop (perf constraint).
        private Collider2D _trigger;
        private Rigidbody2D _body;

        // Guards against a double-load if two trigger callbacks fire on the same frame.
        private bool _transitioning;

        private void Awake()
        {
            // Cache once in Awake — never call GetComponent in Update/FixedUpdate (perf constraint).
            _trigger = GetComponent<Collider2D>();
            _body = GetComponent<Rigidbody2D>();

            // Force the required physics contract so a misconfigured prefab can't silently break.
            _trigger.isTrigger = true;                       // portals are walk-through volumes
            _body.bodyType = RigidbodyType2D.Static;         // the portal never moves; player carries motion
            _body.simulated = true;                          // must be simulated for triggers to register
        }

        private void OnEnable()
        {
            // Subscribe to the portal-unlock callback so the visual flips the instant the objective
            // is satisfied — even if the player is standing nowhere near the portal at that time.
            if (MissionManager.Instance != null)
                MissionManager.Instance.OnLevelCleared += HandleLevelCleared;

            // Initialise the visual to match the current objective state at spawn time.
            RefreshPortalVisual(MissionManager.Instance != null && MissionManager.Instance.IsLevelClear());
        }

        private void OnDisable()
        {
            // Always unsubscribe to avoid dangling delegates after a scene unload.
            if (MissionManager.Instance != null)
                MissionManager.Instance.OnLevelCleared -= HandleLevelCleared;
        }

        // Fired once by MissionManager the moment every required item is collected.
        private void HandleLevelCleared() => RefreshPortalVisual(true);

        /// <summary>Trigger entry — the only place a transition can be initiated.</summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_transitioning) return;

            // LayerMask bit-test: (mask >> layer) & 1. Ignores anything that isn't on the Player layer.
            if ((playerLayer.value & (1 << other.gameObject.layer)) == 0)
                return;

            // Authoritative gate (requirement #2): only teleport when the mission is genuinely clear.
            if (MissionManager.Instance == null || !MissionManager.Instance.IsLevelClear())
                return;

            if (string.IsNullOrEmpty(nextSceneName))
            {
                Debug.LogError("[LevelTeleport] nextSceneName is empty — cannot transition.", this);
                return;
            }

            _transitioning = true;
            if (FungalCurse.Systems.GameManager.Instance != null)
            {
                FungalCurse.Systems.GameManager.Instance.OnLevelComplete(nextSceneName);
            }
            else
            {
                SceneManager.LoadScene(nextSceneName);
            }
        }

        // Swaps the locked/unlocked visual subtrees. Safe when either reference is unassigned.
        private void RefreshPortalVisual(bool unlocked)
        {
            if (lockedVisual != null) lockedVisual.SetActive(!unlocked);
            if (unlockedVisual != null) unlockedVisual.SetActive(unlocked);
        }
    }
}
