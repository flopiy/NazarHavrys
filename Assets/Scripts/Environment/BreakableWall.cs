using UnityEngine;
using FungalCurse.Combat;

namespace FungalCurse.Environment
{
    /// <summary>
    /// "Fungal Curse: Echoes of the Mire" — breakable dark-stone wall masking a hidden enclave.
    ///
    /// Sits over a 3x3 hollow chamber. Implements <see cref="IDamageable"/> so the existing pooled
    /// <see cref="Projectile"/> (or any damage source) can shatter it. When destroyed it simply
    /// disables its renderer + collider, revealing the chamber behind it. No Instantiate/Destroy of
    /// gameplay objects is used at break time.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public sealed class BreakableWall : MonoBehaviour, IDamageable
    {
        [Tooltip("Hit points before the wall shatters.")]
        [SerializeField] private int hitPoints = 2;

        [Tooltip("Renderers hidden when the wall breaks (the dark-stone tilemap chunk).")]
        [SerializeField] private Renderer[] wallRenderers;

        [Tooltip("Colliders disabled when the wall breaks (so the player can walk through).")]
        [SerializeField] private Collider2D[] wallColliders;

        private bool _broken;

        public void TakeDamage(int amount)
        {
            if (_broken) return;

            hitPoints -= Mathf.Max(1, amount);
            if (hitPoints <= 0)
                Break();
        }

        /// <summary>Public so triggers, melee, or debug tools can force the reveal.</summary>
        public void Break()
        {
            if (_broken) return;
            _broken = true;

            if (wallRenderers != null)
                foreach (var r in wallRenderers)
                    if (r != null) r.enabled = false;

            if (wallColliders != null)
                foreach (var c in wallColliders)
                    if (c != null) c.enabled = false;
        }
    }
}
