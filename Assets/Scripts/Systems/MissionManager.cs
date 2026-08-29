using System;
using UnityEngine;

namespace FungalCurse.Systems
{
    /// <summary>
    /// "The Midnight Bramble" — central mission/objective tracker (Singleton).
    ///
    /// SINGLE RESPONSIBILITY: this class ONLY tracks objective progress (collected vs. required items)
    /// and broadcasts when the level objective is satisfied. It contains NO combat, health, UI, or
    /// movement logic — those live in their own components and merely subscribe to / query this one.
    ///
    /// Consumers:
    ///   * Collectibles call <see cref="RegisterItemCollected"/> when picked up.
    ///   * <see cref="FungalCurse.Environment.LevelTeleport"/> queries <see cref="IsLevelClear"/>.
    ///   * UI / VFX subscribe to <see cref="OnProgressChanged"/> and <see cref="OnLevelCleared"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MissionManager : MonoBehaviour
    {
        // ----------------------------------------------------------------
        // Singleton
        // ----------------------------------------------------------------
        // Cached static reference so other systems never need GameObject.Find / FindObjectOfType.
        public static MissionManager Instance { get; private set; }

        [Header("Objective Configuration")]
        [Tooltip("How many items must be collected to clear the level and unlock the portal.")]
        [SerializeField] private int totalRequiredItems = 3;

        [Tooltip("If true, the singleton persists across scene loads. Leave false for per-level objectives.")]
        [SerializeField] private bool persistAcrossScenes = false;

        // Custom internal progress variable. Private set so only RegisterItemCollected can advance it.
        private int currentCollectedItems;

        // ----------------------------------------------------------------
        // Events (the "unlock the portal" callback surface)
        // ----------------------------------------------------------------
        /// <summary>Raised whenever progress changes. Args: (current, total). UI listens here.</summary>
        public event Action<int, int> OnProgressChanged;

        /// <summary>Raised exactly once, the moment the objective is fully satisfied (portal unlock).</summary>
        public event Action OnLevelCleared;

        // Cached "cleared" flag so the OnLevelCleared event fires only once even if extra items arrive.
        private bool levelCleared;

        // Public read-only accessors for other systems / UI.
        public int CurrentCollectedItems => currentCollectedItems;
        public int TotalRequiredItems => totalRequiredItems;

        // ----------------------------------------------------------------
        // Lifecycle
        // ----------------------------------------------------------------
        private void Awake()
        {
            // Enforce the singleton contract. Any duplicate destroys itself so Instance stays valid.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (persistAcrossScenes)
                DontDestroyOnLoad(gameObject);

            // Initialise progress state deterministically at startup.
            currentCollectedItems = 0;
            levelCleared = totalRequiredItems <= 0; // a 0-requirement level is "clear" immediately
        }

        private void OnDestroy()
        {
            // Release the static reference if we are the active instance (prevents dangling pointer).
            if (Instance == this)
                Instance = null;
        }

        // ----------------------------------------------------------------
        // Progress API
        // ----------------------------------------------------------------
        /// <summary>
        /// Called by collectibles when picked up. Advances progress, notifies listeners, and fires the
        /// one-shot portal-unlock event when the requirement is met.
        /// </summary>
        /// <param name="amount">Number of items this pickup is worth (default 1).</param>
        public void RegisterItemCollected(int amount = 1)
        {
            if (levelCleared) return;            // already done — ignore further pickups
            if (amount <= 0) return;             // guard against zero/negative injections

            // Clamp so we can never exceed the required total (keeps UI bars sane).
            currentCollectedItems = Mathf.Min(currentCollectedItems + amount, totalRequiredItems);

            // Notify progress listeners (e.g. a HUD counter) with the fresh values.
            OnProgressChanged?.Invoke(currentCollectedItems, totalRequiredItems);

            // Portal-unlock check: fire the clear callback exactly once.
            if (currentCollectedItems >= totalRequiredItems)
            {
                levelCleared = true;
                OnLevelCleared?.Invoke();
            }
        }

        /// <summary>
        /// Authoritative gate used by <see cref="FungalCurse.Environment.LevelTeleport"/>.
        /// Returns true only when every required item has been collected.
        /// </summary>
        public bool IsLevelClear() => levelCleared;

        /// <summary>Resets objective progress (e.g. on a manual level restart without reloading).</summary>
        public void ResetProgress()
        {
            currentCollectedItems = 0;
            levelCleared = totalRequiredItems <= 0;
            OnProgressChanged?.Invoke(currentCollectedItems, totalRequiredItems);
        }
    }
}
