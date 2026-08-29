using UnityEngine;

namespace FungalCurse.Environment
{
    /// <summary>
    /// "Fungal Curse: Echoes of the Mire" — single-layer infinite (seamless looping) parallax driver.
    ///
    /// SCOPE (single responsibility): scrolls ONE tiled background layer relative to the camera and
    /// loops it perfectly. It caches the camera's initial X and the layer's exact horizontal bounds at
    /// Start, then, when the layer's camera-relative drift exceeds one texture width, steps the anchor
    /// by exactly that width — an invisible jump because the sprite tiles at the same period.
    ///
    /// Runs in LateUpdate so it reads the camera AFTER it has moved this frame (pixel-perfect ordering).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public sealed class InfiniteParallax : MonoBehaviour
    {
        [Header("Tracking")]
        [Tooltip("Camera to follow. Falls back to Camera.main if left empty.")]
        [SerializeField] private Transform cameraTransform;

        [Tooltip("0 = world-locked, 1 = camera-locked (appears infinitely distant).")]
        [Range(0f, 1f)]
        [SerializeField] private float parallaxFactor = 0.5f;

        [Tooltip("If true, the layer wraps horizontally for an endless scroll.")]
        [SerializeField] private bool infiniteHorizontal = true;

        // Cached components / values (resolved once in Start — never in the hot path).
        private SpriteRenderer _sr;
        private float _layerStartX;  // immutable origin used as the relative anchor baseline
        private float _anchorX;      // mutable anchor that steps by one width to loop
        private float _camStartX;    // cached camera initial X (spec: cache initial spatial position)
        private float _y, _z;        // preserved vertical / depth components
        private float _width;        // exact horizontal bounds width of the tiled sprite

        private void Start()
        {
            _sr = GetComponent<SpriteRenderer>();

            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            Vector3 p = transform.position;
            _layerStartX = p.x;
            _anchorX = p.x;
            _y = p.y;
            _z = p.z;

            _camStartX = cameraTransform != null ? cameraTransform.position.x : 0f;

            // Measure the exact horizontal boundary of the (tiled) sprite, accounting for scale/tiling.
            _width = _sr.bounds.size.x;
        }

        private void LateUpdate()
        {
            if (cameraTransform == null) return;

            // Camera displacement from its cached initial X.
            float camDelta = cameraTransform.position.x - _camStartX;

            // Parallax scroll: the layer follows a fraction of the camera's movement.
            float scroll = camDelta * parallaxFactor;
            transform.position = new Vector3(_anchorX + scroll, _y, _z);

            if (!infiniteHorizontal || _width <= 0f) return;

            // 'drift' is how far the layer has fallen behind the camera in the relative frame.
            // The anchor's own offset (anchorRel) tracks accumulated loop steps. Keeping
            // (drift - anchorRel) bounded within ±width yields a perfectly equidistant loop.
            float drift = camDelta * (1f - parallaxFactor);
            float anchorRel = _anchorX - _layerStartX;

            if (drift - anchorRel > _width)
                _anchorX += _width;        // camera moved right past one tile -> step anchor right
            else if (drift - anchorRel < -_width)
                _anchorX -= _width;        // camera moved left past one tile -> step anchor left
        }
    }
}
