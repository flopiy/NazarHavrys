using UnityEngine;
using UnityEngine.SceneManagement;
using FungalCurse.Combat;

namespace FungalCurse.Environment
{
    /// <summary>
    /// "Fungal Curse: Echoes of the Mire" — boundary trigger that resolves one of two zone behaviours
    /// against the Player.
    ///
    /// SCOPE (single responsibility): this component ONLY classifies a zone (via the internal enum) and
    /// dispatches the matching outcome. It owns no health math (it requests a Kill through
    /// <see cref="IKillable"/>) and no scene-build logic beyond the load call.
    ///
    ///   * GreenInstantDeath  -> un-mitigated elimination of the player (IKillable.Kill).
    ///   * BlueLevelComplete  -> SceneManager.LoadScene(nextSceneName).
    ///
    /// Player identification uses a LayerMask bit-test (allocation-free), not tag-string comparison.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]   // explicit scene-safety: guarantees trigger events fire
    [RequireComponent(typeof(Collider2D))]    // the trigger volume
    [DisallowMultipleComponent]
    public sealed class LevelZoneTrigger : MonoBehaviour
    {
        /// <summary>The two boundary behaviours this trigger can represent.</summary>
        public enum ZoneType
        {
            GreenInstantDeath,
            BlueLevelComplete
        }

        [Header("Zone Behaviour")]
        [SerializeField] private ZoneType zoneType = ZoneType.GreenInstantDeath;

        [Header("Player Detection")]
        [Tooltip("Layer(s) considered 'the Player'. Set to your Player layer in the Inspector.")]
        [SerializeField] private LayerMask playerLayer;

        [Header("Blue (Level Complete) Settings")]
        [Tooltip("Scene to load when a BlueLevelComplete zone is entered (must be in Build Settings).")]
        [SerializeField] private string nextSceneName = "Level_02";

        // Cached components (resolved once in Awake — never in the hot path).
        private Collider2D _trigger;
        private Rigidbody2D _body;

        // One-shot latch so a single entry can't fire the outcome twice.
        private bool _consumed;

        private void Awake()
        {
            _trigger = GetComponent<Collider2D>();
            _body = GetComponent<Rigidbody2D>();

            // Enforce the required physics contract so a misconfigured prefab can't silently fail.
            _trigger.isTrigger = true;                  // boundary zones are walk-into volumes
            _body.bodyType = RigidbodyType2D.Static;    // the zone never moves; player carries motion
            _body.simulated = true;                     // must be simulated for triggers to register
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_consumed) return;

            // LayerMask bit-test: ignore anything that is not on the configured Player layer.
            if ((playerLayer.value & (1 << other.gameObject.layer)) == 0)
                return;

            switch (zoneType)
            {
                case ZoneType.GreenInstantDeath:
                    // Search parent chain so a child collider still resolves the player's root script.
                    IKillable killable = other.GetComponentInParent<IKillable>();
                    if (killable != null)
                    {
                        _consumed = true;
                        killable.Kill(); // immediate, un-mitigated elimination
                    }
                    break;

                case ZoneType.BlueLevelComplete:
                    if (string.IsNullOrEmpty(nextSceneName))
                    {
                        Debug.LogError("[LevelZoneTrigger] nextSceneName is empty — cannot complete level.", this);
                        return;
                    }
                    _consumed = true;
                    if (FungalCurse.Systems.GameManager.Instance != null)
                    {
                        FungalCurse.Systems.GameManager.Instance.OnLevelComplete(nextSceneName);
                    }
                    else
                    {
                        SceneManager.LoadScene(nextSceneName);
                    }
                    break;
            }
        }
    }
}
