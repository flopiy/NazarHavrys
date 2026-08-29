using UnityEngine;

namespace FungalCurse.Combat
{
    /// <summary>
    /// Self-contained cosmetic driver for the wizard's Magic Shield bubble. Slowly rotates and
    /// gently pulses the scale + alpha while active, giving the barrier a "living energy" feel.
    /// Also dynamically clips the shield at the ground level using a runtime SpriteMask so that
    /// the bubble never intersects or clips below solid platforms, resting perfectly on them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShieldVisual : MonoBehaviour
    {
        [Header("Cosmetics")]
        [SerializeField] private float spinSpeed = 35f;
        [SerializeField] private float pulseSpeed = 4.5f;
        [SerializeField] private float pulseAmount = 0.05f;
        [SerializeField] private float minAlpha = 0.55f;
        [SerializeField] private float maxAlpha = 0.95f;

        [Header("Clipping")]
        [SerializeField] private LayerMask groundLayer;
        [Tooltip("Maximum distance below the shield center to check for the platform floor.")]
        [SerializeField] private float groundCheckDistance = 2.5f;

        private SpriteRenderer _sr;
        private Vector3 _baseScale;
        
        private SpriteMask _spriteMask;
        private Transform _maskTransform;
        private const float MaskHeight = 8f;

        private void Awake()
        {
            _sr = GetComponentInChildren<SpriteRenderer>();
            _baseScale = transform.localScale;

            // Fallback to Ground/Default if layer mask is not assigned
            if (groundLayer.value == 0)
            {
                groundLayer = LayerMask.GetMask("Ground", "Default");
            }

            BuildRuntimeMask();
        }

        // Dynamically build the SpriteMask child so we don't depend on pre-built sub-assets
        private void BuildRuntimeMask()
        {
            GameObject maskGo = new GameObject("ShieldMask");
            maskGo.transform.SetParent(transform, false);
            _maskTransform = maskGo.transform;

            _spriteMask = maskGo.AddComponent<SpriteMask>();
            
            // Generate a 1x1 solid white sprite at runtime
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            Sprite squareSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            
            _spriteMask.sprite = squareSprite;
            
            // Scale the mask to cover a wide horizontal area and high vertical area (above ground)
            _maskTransform.localScale = new Vector3(12f, MaskHeight, 1f);
        }

        private void OnEnable()
        {
            // Reset to a clean baseline every time the shield is raised.
            transform.localScale = _baseScale;
            transform.localRotation = Quaternion.identity;
        }

        private void Update()
        {
            // Gentle spinning
            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

            // Gentle pulsing
            float wave = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f; // 0..1
            transform.localScale = _baseScale * (1f + (wave - 0.5f) * 2f * pulseAmount);

            if (_sr != null)
            {
                Color c = _sr.color;
                c.a = Mathf.Lerp(minAlpha, maxAlpha, wave);
                _sr.color = c;
            }

            ApplyGroundClipping();
        }

        // Raycasts downwards to find the platform surface and positions the SpriteMask to clip the bottom
        private void ApplyGroundClipping()
        {
            if (_spriteMask == null || _sr == null) return;

            // Cast straight down from the shield center
            Vector2 origin = transform.position;
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDistance, groundLayer);

            if (hit.collider != null)
            {
                float groundY = hit.point.y;
                float localGroundY = groundY - transform.position.y;

                // Center of the mask needs to be positioned so its bottom aligns with the ground.
                // Since mask scale.y is MaskHeight, the bottom edge is at -MaskHeight/2 relative to mask center.
                // So mask center local Y = localGroundY + MaskHeight/2.
                _maskTransform.localPosition = new Vector3(0f, localGroundY + (MaskHeight * 0.5f), 0f);

                // Enable clipping on the sprite renderer
                _sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }
            else
            {
                // In the air: render the full circular shield without any ground clipping
                _sr.maskInteraction = SpriteMaskInteraction.None;
            }
        }
    }
}
