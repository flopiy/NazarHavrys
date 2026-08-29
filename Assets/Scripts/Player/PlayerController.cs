using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FungalCurse.Player
{
    /// <summary>
    /// "Fungal Curse: Echoes of the Mire" — Architectural Input & Movement Core.
    ///
    /// Design contract (hard rules enforced by this class):
    ///   * Horizontal movement is injected DIRECTLY into Rigidbody2D.linearVelocity.
    ///     Rigidbody2D.AddForce() is NEVER used for horizontal locomotion (crisp, deterministic feel).
    ///   * Sprite facing is flipped with SpriteRenderer.flipX ONLY. transform.localScale is never
    ///     touched, so child transforms (2D Lights, checkpoint anchors, muzzle points) keep their
    ///     local positions intact.
    ///   * Vertical feel is driven by DYNAMIC gravity multipliers (fallMultiplier / lowJumpMultiplier),
    ///     not by inflating Rigidbody2D.gravityScale. gravityScale stays at the calibrated base value.
    ///
    /// Requires the New Input System. Wire the project "InputSystem_Actions" asset into the
    /// 'Input Actions' field; the controller resolves the Player map actions by name at runtime,
    /// so it does not depend on the generated C# wrapper being enabled.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour
    {
        // ---------------------------------------------------------------------
        // Inspector configuration
        // ---------------------------------------------------------------------
        [Header("Input")]
        [Tooltip("Drag the project 'InputSystem_Actions' asset here.")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Horizontal Movement")]
        [Tooltip("Target horizontal speed in units/second. Injected directly into velocity.")]
        [SerializeField] private float moveSpeed = 8.5f;
        [Tooltip("How quickly velocity reaches the target on the ground (units/sec^2).")]
        [SerializeField] private float groundAcceleration = 120f;
        [Tooltip("How quickly velocity reaches the target while airborne (units/sec^2).")]
        [SerializeField] private float airAcceleration = 55f;

        [Header("Jump")]
        [Tooltip("Initial upward velocity applied on jump (units/second).")]
        [SerializeField] private float jumpVelocity = 17f;

        // --- Hardcoded timing windows from the design document ---
        // Jump Buffering window: a jump press within this window before landing still fires on touchdown.
        private const float JumpBufferTime = 0.12f;
        // Coyote Time window: jump remains valid for this long after leaving a ledge.
        private const float CoyoteTime = 0.14f;
        // Dynamic gravity multipliers (replace any temptation to crank gravityScale).
        private const float FallMultiplier = 4.8f;     // applied while descending
        private const float LowJumpMultiplier = 3.5f;  // applied while rising with jump released

        [Header("Base Gravity")]
        [Tooltip("Calibrated base gravity scale. Multipliers below are layered ON TOP of this; " +
                 "do not inflate this value to fake heavy falls.")]
        [SerializeField] private float baseGravityScale = 3f;

        [Header("Ground Detection")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundCheckRadius = 0.12f;
        [SerializeField] private LayerMask groundLayer;

        [Header("Dash")]
        [Tooltip("Dash travel speed (units/second).")]
        [SerializeField] private float dashSpeed = 26f;
        [Tooltip("Duration of the dash burst AND the i-frame window (seconds).")]
        [SerializeField] private float dashDuration = 0.16f;
        [Tooltip("Cooldown before the next dash is allowed (seconds).")]
        [SerializeField] private float dashCooldown = 0.45f;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        // ---------------------------------------------------------------------
        // Cached components & input actions
        // ---------------------------------------------------------------------
        private Rigidbody2D _rb;
        private InputActionMap _playerMap;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _dashAction;

        // ---------------------------------------------------------------------
        // Runtime state
        // ---------------------------------------------------------------------
        private float _moveInputX;
        private bool _isGrounded;
        private bool _facingRight = true;

        private float _coyoteTimer;       // counts DOWN from CoyoteTime while grounded resets it
        private float _jumpBufferTimer;   // counts DOWN from JumpBufferTime after a jump press

        private bool _isDashing;
        private bool _canDash = true;

        // Cached physics layer indices for the dash i-frame toggle.
        private int _playerLayer;
        private int _enemyLayer;

        // ---------------------------------------------------------------------
        // Unity lifecycle
        // ---------------------------------------------------------------------
        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            // Force the calibrated base gravity. We never raise this to fake heavier falls;
            // the dynamic multipliers in FixedUpdate do that job.
            _rb.gravityScale = baseGravityScale;
            // Continuous interpolation keeps pixel movement smooth between physics steps.
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.freezeRotation = true;

            // Resolve layers used for dash invulnerability.
            _playerLayer = LayerMask.NameToLayer("Player");
            _enemyLayer = LayerMask.NameToLayer("Enemies");
            if (_playerLayer < 0 || _enemyLayer < 0)
            {
                Debug.LogError("[PlayerController] 'Player' and/or 'Enemies' layers are missing. " +
                               "Dash i-frames will be disabled until both layers exist.", this);
            }

            ResolveInputActions();
        }

        private void ResolveInputActions()
        {
            if (inputActions == null)
            {
                Debug.LogError("[PlayerController] Input Actions asset is not assigned.", this);
                return;
            }

            _playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
            _moveAction = _playerMap.FindAction("Move", throwIfNotFound: true);
            _jumpAction = _playerMap.FindAction("Jump", throwIfNotFound: true);
            _dashAction = _playerMap.FindAction("Dash", throwIfNotFound: true);
        }

        private void OnEnable()
        {
            if (_playerMap == null) return;

            _playerMap.Enable();
            _jumpAction.performed += OnJumpPerformed;
            _dashAction.performed += OnDashPerformed;
        }

        private void OnDisable()
        {
            if (_playerMap == null) return;

            _jumpAction.performed -= OnJumpPerformed;
            _dashAction.performed -= OnDashPerformed;
            _playerMap.Disable();
        }

        // ---------------------------------------------------------------------
        // Input callbacks
        // ---------------------------------------------------------------------
        private void OnJumpPerformed(InputAction.CallbackContext ctx)
        {
            // Open the jump buffer window. The actual jump fires in FixedUpdate when
            // (buffer active) AND (grounded OR within coyote time).
            _jumpBufferTimer = JumpBufferTime;
        }

        private void OnDashPerformed(InputAction.CallbackContext ctx)
        {
            if (_canDash && !_isDashing)
                StartCoroutine(DashRoutine());
        }

        // ---------------------------------------------------------------------
        // Per-frame input sampling & timers (Update) — physics in FixedUpdate
        // ---------------------------------------------------------------------
        private void Update()
        {
            if (_moveAction != null)
                _moveInputX = _moveAction.ReadValue<Vector2>().x;

            // Tick down the buffer/coyote timers in real frame time.
            if (_jumpBufferTimer > 0f) _jumpBufferTimer -= Time.deltaTime;
            if (_coyoteTimer > 0f) _coyoteTimer -= Time.deltaTime;

            HandleFacing();
        }

        private void FixedUpdate()
        {
            UpdateGrounded();

            // While dashing, the coroutine fully owns velocity — skip locomotion & gravity shaping.
            if (_isDashing) return;

            ApplyHorizontalMovement();
            TryConsumeBufferedJump();
            ApplyDynamicGravity();
        }

        // ---------------------------------------------------------------------
        // Movement implementation
        // ---------------------------------------------------------------------
        private void UpdateGrounded()
        {
            bool wasGrounded = _isGrounded;
            _isGrounded = groundCheck != null &&
                          Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

            // Refresh coyote window whenever we are on the ground.
            if (_isGrounded)
                _coyoteTimer = CoyoteTime;
            else if (wasGrounded)
                // Just walked off a ledge: start the coyote countdown from "now".
                _coyoteTimer = CoyoteTime;
        }

        /// <summary>
        /// Direct velocity injection — NEVER AddForce. Acceleration smoothing keeps the feel
        /// crisp while avoiding instantaneous teleport-like snaps.
        /// </summary>
        private void ApplyHorizontalMovement()
        {
            float targetX = _moveInputX * moveSpeed;
            float accel = _isGrounded ? groundAcceleration : airAcceleration;

            float newX = Mathf.MoveTowards(_rb.linearVelocity.x, targetX, accel * Time.fixedDeltaTime);
            _rb.linearVelocity = new Vector2(newX, _rb.linearVelocity.y);
        }

        private void TryConsumeBufferedJump()
        {
            // Jump fires when a buffered press coincides with being grounded OR within coyote time.
            bool canJump = _jumpBufferTimer > 0f && _coyoteTimer > 0f;
            if (!canJump) return;

            // Direct vertical velocity set guarantees a consistent jump height regardless of
            // residual downward velocity from a previous fall.
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpVelocity);

            // Consume both windows so a single press can't double-fire.
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
        }

        /// <summary>
        /// Dynamic gravity shaping. gravityScale stays at baseGravityScale; we add extra downward
        /// velocity each step based on the phase of the jump arc.
        /// </summary>
        private void ApplyDynamicGravity()
        {
            float verticalVel = _rb.linearVelocity.y;
            bool jumpHeld = _jumpAction != null && _jumpAction.IsPressed();

            if (verticalVel < 0f)
            {
                // Falling: pull down harder for a snappy, weighty descent.
                _rb.linearVelocity += Vector2.up * Physics2D.gravity.y *
                                      (FallMultiplier - 1f) * Time.fixedDeltaTime;
            }
            else if (verticalVel > 0f && !jumpHeld)
            {
                // Rising but the jump button was released: cut the jump short (variable jump height).
                _rb.linearVelocity += Vector2.up * Physics2D.gravity.y *
                                      (LowJumpMultiplier - 1f) * Time.fixedDeltaTime;
            }
        }

        // ---------------------------------------------------------------------
        // Facing — SpriteRenderer.flipX ONLY (never transform.localScale)
        // ---------------------------------------------------------------------
        private void HandleFacing()
        {
            if (Mathf.Abs(_moveInputX) < 0.01f) return;

            bool shouldFaceRight = _moveInputX > 0f;
            if (shouldFaceRight == _facingRight) return;

            _facingRight = shouldFaceRight;
            if (spriteRenderer != null)
                // flipX leaves the transform untouched, so child Lights/checkpoints stay put.
                spriteRenderer.flipX = !_facingRight;
        }

        // ---------------------------------------------------------------------
        // Dash with structural temporary immunity (I-Frames)
        // ---------------------------------------------------------------------
        /// <summary>
        /// Performs a fixed-distance dash in the facing direction. For the duration of the dash,
        /// collisions between the "Player" and "Enemies" physics layers are disabled via
        /// Physics2D.IgnoreLayerCollision, giving STRUCTURAL invulnerability (the bodies physically
        /// cannot touch), not just a damage flag. Layer collision is always restored afterwards.
        /// </summary>
        private IEnumerator DashRoutine()
        {
            _isDashing = true;
            _canDash = false;

            bool iFramesActive = _playerLayer >= 0 && _enemyLayer >= 0;
            if (iFramesActive)
                Physics2D.IgnoreLayerCollision(_playerLayer, _enemyLayer, true);

            // Suspend gravity so the dash stays perfectly horizontal and frame-tight.
            float cachedGravity = _rb.gravityScale;
            _rb.gravityScale = 0f;

            float direction = _facingRight ? 1f : -1f;
            _rb.linearVelocity = new Vector2(direction * dashSpeed, 0f);

            yield return new WaitForSeconds(dashDuration);

            // Restore physics state and clear the i-frame window.
            _rb.gravityScale = cachedGravity;
            if (iFramesActive)
                Physics2D.IgnoreLayerCollision(_playerLayer, _enemyLayer, false);
            _isDashing = false;

            // Cooldown gate.
            yield return new WaitForSeconds(dashCooldown);
            _canDash = true;
        }

        // Safety net: if the object is disabled mid-dash, never leave layers permanently ignoring.
        private void OnDisableRestoreCollisions()
        {
            if (_playerLayer >= 0 && _enemyLayer >= 0)
                Physics2D.IgnoreLayerCollision(_playerLayer, _enemyLayer, false);
        }

        private void OnDestroy() => OnDisableRestoreCollisions();

        // ---------------------------------------------------------------------
        // Editor gizmos
        // ---------------------------------------------------------------------
        private void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
