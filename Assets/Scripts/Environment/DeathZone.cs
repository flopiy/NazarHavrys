using UnityEngine;
using UnityEngine.SceneManagement;
using FungalCurse.Systems;

namespace FungalCurse.Environment
{
    /// <summary>
    /// "Fungal Curse: Echoes of the Mire" — the bottom-of-the-world kill plane.
    ///
    /// SINGLE RESPONSIBILITY: this component ONLY detects the player falling out of the level and
    /// reloads the active scene. It carries no health, combat, scoring, or UI logic — those belong
    /// to their own components.
    ///
    /// Placement:
    ///   A wide, thin trigger spanning the entire bottom boundary vector of the grid map. Anything on
    ///   the configured 'Player' layer that enters it has fallen off the level and triggers a reload.
    ///
    /// Detection model (mirrors <see cref="LevelTeleport"/>):
    ///   * STATIC trigger zone; the Player owns the moving Rigidbody2D that drives the callback.
    ///   * LayerMask bit-test instead of tag string compare (cheap, allocation-free).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]   // explicit scene-safety: guarantees trigger events fire
    [RequireComponent(typeof(Collider2D))]    // the kill-plane trigger volume
    [DisallowMultipleComponent]
    public sealed class DeathZone : MonoBehaviour
    {
        [Header("Player Detection")]
        [Tooltip("Layer(s) considered 'the Player'. Set this to your Player layer in the Inspector.")]
        [SerializeField] private LayerMask playerLayer;

        [Header("Respawn Behaviour")]
        [Tooltip("If true, MissionManager progress is reset before the scene reloads, so a fall " +
                 "restarts the objective from scratch. If false, collected items survive the reload " +
                 "only if the MissionManager persists across scenes.")]
        [SerializeField] private bool resetObjectiveOnDeath = true;

        // Cached components — resolved once in Awake, never in the hot loop (perf constraint).
        private Collider2D _trigger;
        private Rigidbody2D _body;

        // Guards against issuing two reloads if multiple contacts land on the same frame.
        private bool _reloading;

        private void Awake()
        {
            _trigger = GetComponent<Collider2D>();
            _body = GetComponent<Rigidbody2D>();

            // Enforce the required physics contract so a misconfigured prefab can't silently fail.
            _trigger.isTrigger = true;                       // kill plane is a walk-into volume
            _body.bodyType = RigidbodyType2D.Static;         // the plane never moves
            _body.simulated = true;                          // must be simulated for triggers to register
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_reloading) return;

            // LayerMask bit-test: only the Player layer triggers a death.
            if ((playerLayer.value & (1 << other.gameObject.layer)) == 0)
                return;

            _reloading = true;

            // Optionally wipe objective progress so the reloaded level starts fresh.
            if (resetObjectiveOnDeath && MissionManager.Instance != null)
                MissionManager.Instance.ResetProgress();

            // Find the player's killable component and trigger the proper death logic (death animation, life loss, reload)
            FungalCurse.Combat.IKillable killable = other.GetComponentInParent<FungalCurse.Combat.IKillable>();
            if (killable != null)
            {
                killable.Kill();
                Debug.Log($"[DeathZone] Player fell below platforms. Triggered IKillable.Kill on {other.gameObject.name}.");
            }
            else
            {
                // Fallback scene reload if player has no IKillable component
                Scene active = SceneManager.GetActiveScene();
                SceneManager.LoadScene(active.buildIndex);
            }
        }
    }
}
