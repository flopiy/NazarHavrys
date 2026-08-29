using UnityEngine;
using FungalCurse.Systems;

namespace FungalCurse.Environment
{
    /// <summary>
    /// "Fungal Curse: Echoes of the Mire" — a single objective pickup.
    ///
    /// SCOPE (single responsibility): on player contact it reports exactly one collected item to the
    /// <see cref="MissionManager"/> and removes itself. No scoring, combat, or UI logic lives here.
    ///
    /// Player identification uses a LayerMask bit-test (allocation-free), not tag-string comparison.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public sealed class Collectible : MonoBehaviour
    {
        [Header("Player Detection")]
        [Tooltip("Layer(s) considered 'the Player'.")]
        [SerializeField] private LayerMask playerLayer;

        [Tooltip("How many objective items this pickup is worth.")]
        [SerializeField] private int worth = 1;

        private Collider2D _trigger;
        private bool _collected;

        private void Awake()
        {
            _trigger = GetComponent<Collider2D>();
            _trigger.isTrigger = true; // pickups are walk-through volumes
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_collected) return;

            // LayerMask bit-test: ignore anything that is not on the Player layer.
            if ((playerLayer.value & (1 << other.gameObject.layer)) == 0)
                return;

            if (MissionManager.Instance != null)
            {
                _collected = true;
                MissionManager.Instance.RegisterItemCollected(worth);
                Destroy(gameObject);
            }
        }
    }
}
