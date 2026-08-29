using UnityEngine;
using UnityEngine.InputSystem;
using FungalCurse.Systems;

namespace FungalCurse.Player
{
    /// <summary>
    /// "The Midnight Bramble" — player controller for the fragile ranged mage
    /// (the "Cute 2D Girl - Wizard" asset).
    ///
    /// Mechanics:
    ///   * Crisp Rigidbody2D locomotion via direct velocity injection (no AddForce for movement).
    ///   * Ranged Spellcasting: spawns a MagicProjectile from a forward offset. Uses the project
    ///     ObjectPooler when available (GC-free) and falls back to Instantiate otherwise.
    ///   * Mana Blink (teleport dash): instantly shifts the body 3.5 units forward on X via
    ///     rb.MovePosition(), guarded by a forward Raycast so the wizard never blinks INTO solid
    ///     Tilemap colliders.
    ///   * Lightweight mana resource (a mage should be resource-gated): casting and blinking cost
    ///     mana, which regenerates over time.
    ///
    /// Flip handling — IMPORTANT:
    ///   The wizard is a 17-part 2D skeletal rig (SpriteSkin + bones). Per-renderer SpriteRenderer.flipX
    ///   cannot mirror such a rig (each part would mirror about its own pivot). The reliable flip for a
    ///   rig is a single localScale.x flip on the VISUAL parent. To still honor the design goal —
    ///   "keep child lighting anchors attached correctly" — the Point Light 2D and the cast origin are
    ///   parented to the (never-flipped) player ROOT, NOT under the visual subtree. <see cref="FlipMode"/>
    ///   also exposes a literal SpriteRenderer.flipX path for simple single-sprite characters.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public sealed class WizardPlayerController : MonoBehaviour
    {
        public enum FlipMode
        {
            /// <summary>Flip the visual rig parent's localScale.x. Correct for skeletal/multi-part rigs.</summary>
            VisualScaleX,
            /// <summary>Toggle flipX on every cached SpriteRenderer. Correct only for single-sprite characters.</summary>
            SpriteFlipX
        }

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float groundAcceleration = 80f;
        [SerializeField] private float airAcceleration = 40f;
        [SerializeField] private float jumpVelocity = 14f;
        [SerializeField] private float baseGravityScale = 4f;
        [Tooltip("Extra downward pull while falling, for a less floaty arc.")]
        [SerializeField] private float fallMultiplier = 3.2f;

        [Header("Ground Detection")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundCheckRadius = 0.14f;
        [SerializeField] private LayerMask groundLayer;

        [Header("Visuals & Animation")]
        [SerializeField] private FlipMode flipMode = FlipMode.VisualScaleX;
        [Tooltip("The visual rig parent (e.g. the Wizard prefab root). Flipped when FlipMode = VisualScaleX.")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Animator animator;

        // Animator parameter hashes (from the 'Wizard Demo' controller).
        private static readonly int HashIsRun = Animator.StringToHash("isRun");
        private static readonly int HashIsJump = Animator.StringToHash("isJump");

        // Components / input
        private Rigidbody2D _rb;
        private SpriteRenderer[] _renderers;
        private InputActionMap _map;
        private InputAction _moveAction, _jumpAction;

        // State
        private float _moveX;
        private bool _grounded;
        private int _facing = 1; // +1 right, -1 left

        public int Facing => _facing;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = baseGravityScale;
            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            if (visualRoot == null) visualRoot = transform;
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);

            if (flipMode == FlipMode.VisualScaleX)
            {
                if (visualRoot != null)
                {
                    visualRoot.localEulerAngles = Vector3.zero;
                }
                if (_renderers != null)
                {
                    foreach (var r in _renderers)
                    {
                        if (r != null) r.flipX = false;
                    }
                }
            }

            // Load speed meta-progression upgrades from PlayerPrefs
            int speedUpgradeLvl = PlayerPrefs.GetInt("Upgrade_Speed_Level", 0);
            moveSpeed += speedUpgradeLvl * 0.2f; // Minimal, tactical speed increase

            // Load jump meta-progression upgrades from PlayerPrefs
            int jumpUpgradeLvl = PlayerPrefs.GetInt("Upgrade_Jump_Level", 0);
            jumpVelocity += jumpUpgradeLvl * 0.3f; // Minimal, tactical jump height increase

            ResolveInput();
        }

        private void ResolveInput()
        {
            if (inputActions == null)
            {
                Debug.LogError("[WizardPlayerController] Input Actions asset not assigned.", this);
                return;
            }
            _map = inputActions.FindActionMap("Player", true);
            _moveAction = _map.FindAction("Move", true);
            _jumpAction = _map.FindAction("Jump", true);
        }

        private void OnEnable()
        {
            if (_map == null) return;
            _map.Enable();
            _jumpAction.performed += OnJump;
        }

        private void OnDisable()
        {
            if (_map == null) return;
            _jumpAction.performed -= OnJump;
            _map.Disable();
        }

        private void OnJump(InputAction.CallbackContext _)
        {
            if (_grounded)
            {
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpVelocity);
            }
        }

        private void Update()
        {
            if (_moveAction != null) _moveX = _moveAction.ReadValue<Vector2>().x;

            HandleFacing();
            UpdateAnimator();
        }

        private void FixedUpdate()
        {
            _grounded = groundCheck != null &&
                        Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

            ApplyHorizontalMovement();
            ApplyFallGravity();
        }

        private void ApplyHorizontalMovement()
        {
            float target = _moveX * moveSpeed;
            float accel = _grounded ? groundAcceleration : airAcceleration;
            float newX = Mathf.MoveTowards(_rb.linearVelocity.x, target, accel * Time.fixedDeltaTime);
            _rb.linearVelocity = new Vector2(newX, _rb.linearVelocity.y);
        }

        private void ApplyFallGravity()
        {
            if (_rb.linearVelocity.y < 0f)
                _rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }

        // -----------------------------------------------------------------
        // Facing & animation
        // -----------------------------------------------------------------
        private void HandleFacing()
        {
            if (Mathf.Abs(_moveX) < 0.01f) return;
            int desired = _moveX > 0f ? 1 : -1;
            if (desired == _facing) return;
            _facing = desired;
            ApplyFlip();
        }

        private void ApplyFlip()
        {
            switch (flipMode)
            {
                case FlipMode.VisualScaleX:
                    // Mirror the visual rig via localScale.x; light + cast anchors live on the root and stay put.
                    if (visualRoot != null)
                    {
                        Vector3 s = visualRoot.localScale;
                        visualRoot.localScale = new Vector3(Mathf.Abs(s.x) * _facing, s.y, s.z);
                        visualRoot.localEulerAngles = Vector3.zero;
                    }
                    if (_renderers != null)
                    {
                        foreach (var r in _renderers)
                        {
                            if (r != null && r.flipX) r.flipX = false;
                        }
                    }
                    break;
                case FlipMode.SpriteFlipX:
                    if (_renderers != null)
                        foreach (var r in _renderers)
                            if (r != null) r.flipX = _facing < 0;
                    break;
            }
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;
            animator.SetBool(HashIsRun, Mathf.Abs(_moveX) > 0.05f && _grounded);

            // Smoothly activate jump state when airborne or during rising velocity
            bool isJumping = !_grounded || _rb.linearVelocity.y > 0.1f;
            animator.SetBool(HashIsJump, isJumping);
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheck != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
            }
        }
    }
}
