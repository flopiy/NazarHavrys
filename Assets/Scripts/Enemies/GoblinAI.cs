using System.Collections.Generic;
using UnityEngine;

namespace FungalCurse.Enemies
{
    /// <summary>
    /// "Fungal Curse: Echoes of the Mire" — Detached Finite State Machine for the Goblin Thief.
    ///
    /// The machine is fully de-coupled: a single <see cref="State"/> enum drives a switch in
    /// FixedUpdate, every transition flows through <see cref="ChangeState"/> (so enter/exit logic
    /// lives in exactly one place), and all per-state timing is data-driven by constants pulled from
    /// the design document.
    ///
    /// Patrol      : walks along ledges. Edge + wall raycasts (Vector2.down / Vector2.right) make the
    ///               goblin respect Pixel Tiles Pack grid limits automatically — it turns at gaps and
    ///               walls instead of marching off the tilemap.
    /// Anticipation: 0.25s static tell before the strike (telegraph).
    /// ActiveStrike: 0.10s physical dash toward the player's last-seen position.
    /// Recovery    : 0.45s absolute dead-zone — the goblin is idle and fully vulnerable.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public sealed class GoblinAI : MonoBehaviour
    {
        // ---------------------------------------------------------------------
        // De-coupled state definition
        // ---------------------------------------------------------------------
        public enum State
        {
            Patrol,
            Anticipation,
            ActiveStrike,
            Recovery
        }

        // --- Hardcoded combat timers (seconds) from the design document ---
        [Header("FSM Tuning")]
        [SerializeField] private float anticipationDuration = 0.35f;
        [SerializeField] private float recoveryDuration = 0.8f;

        [Header("Targeting")]
        [SerializeField] private Transform player;
        [Tooltip("Horizontal distance at which the goblin commits to an attack.")]
        [SerializeField] private float aggroRange = 4.5f;
        [Tooltip("Vertical tolerance so the goblin ignores the player on far-away platforms.")]
        [SerializeField] private float verticalTolerance = 1.25f;

        [Header("Patrol Movement")]
        [SerializeField] private float patrolSpeed = 2.2f;

        [Header("Strike")]
        [Tooltip("Horizontal dash speed during the ActiveStrike phase.")]
        [SerializeField] private float strikeSpeed = 11f;

        [Header("Ground / Wall Probing (Pixel Tiles grid awareness)")]
        [Tooltip("Origin for the downward edge ray. Place at the front-bottom of the goblin.")]
        [SerializeField] private Transform edgeCheck;
        [Tooltip("Origin for the forward wall ray. Place at body height, at the front.")]
        [SerializeField] private Transform wallCheck;
        [SerializeField] private float edgeCheckDistance = 0.6f;
        [SerializeField] private float wallCheckDistance = 0.25f;
        [Tooltip("Tilemap / solid ground layer used by both probes.")]
        [SerializeField] private LayerMask groundLayer;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Tooltip("The prefix for animator states (e.g., 'Goblin' or 'Skeleton'). Autodetected if empty.")]
        [SerializeField] private string animPrefix = "";

        // ---------------------------------------------------------------------
        // Runtime
        // ---------------------------------------------------------------------
        private Rigidbody2D _rb;
        private Animator _animator;
        private Transform _visualRoot;
        private bool _flipsRoot;             // true when there is no Visual child, so the whole root is flipped
        private Transform _uiCanvas;         // world-space health-bar canvas to keep upright when the root flips
        private float _uiCanvasBaseScaleX = 1f;
        private State _state = State.Patrol;
        private float _stateTimer;          // counts UP since the current state was entered
        private int _facingDir = 1;         // +1 right, -1 left
        private Vector2 _strikeDirection;   // locked-in at the start of ActiveStrike

        // Cache for animation lengths to prevent cutoffs
        private float _currentAttackDuration = 0.4f;

        // ---------------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------------
        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.freezeRotation = true;

            // Locate the pre-authored visual container that holds the Animator, every
            // SpriteRenderer/SpriteSkin part and the whole bone hierarchy. Flipping ONLY this
            // container mirrors the 2D skeletal rig without ever touching the Rigidbody2D,
            // colliders, ground/wall probes or the health-bar canvas (those stay on the root).
            //
            // IMPORTANT: this class intentionally performs NO runtime re-parenting, NO
            // DestroyImmediate and NO reflection on SpriteSkin internals. Those patterns corrupt
            // Unity's 2D Animation deformation system and are the reason the rigs "fell apart" in
            // standalone player builds. The container is authored into the prefab instead.
            _visualRoot = transform.Find("Visual");
            if (_visualRoot == null) _visualRoot = transform.Find("VisualRoot");

            _flipsRoot = _visualRoot == null;
            if (_visualRoot == null)
                _visualRoot = transform; // graceful fallback: flip the whole object

            _animator = GetComponentInChildren<Animator>(true);

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            // When the whole root is flipped, any world-space UI (the health-bar canvas) would be
            // mirrored too. Cache it so we can counter-flip it and keep the slider/text upright.
            if (_flipsRoot)
            {
                var canvas = GetComponentInChildren<Canvas>(true);
                if (canvas != null)
                {
                    _uiCanvas = canvas.transform;
                    _uiCanvasBaseScaleX = Mathf.Abs(_uiCanvas.localScale.x);
                }
            }

            if (string.IsNullOrEmpty(animPrefix))
            {
                bool isSkeleton = gameObject.name.Contains("Skeleton") || 
                                  gameObject.name.Contains("Skel") || 
                                  gameObject.name.Contains("Mire_Boss");

                if (!isSkeleton && _animator != null && _animator.runtimeAnimatorController != null)
                {
                    string controllerName = _animator.runtimeAnimatorController.name.ToLower();
                    if (controllerName.Contains("skeleton"))
                    {
                        isSkeleton = true;
                    }
                }

                animPrefix = isSkeleton ? "Skeleton" : "Goblin";
            }

            // Setup specific timings based on unit type
            if (animPrefix == "Skeleton")
            {
                _currentAttackDuration = 0.90f; // Skeleton_Attack is ~0.97s
                strikeSpeed = 6.0f;             // Regular axe swing movement
                anticipationDuration = 0.35f;
            }
            else
            {
                _currentAttackDuration = 0.75f;  // Goblin_Attack is ~0.82s
                strikeSpeed = 10.0f;             // Quick dash
                anticipationDuration = 0.20f;
            }

            if (player == null)
            {
                // Resolve by tag without a fragile GameObject.Find name lookup.
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) player = tagged.transform;
            }

            // Scaled aggro range for bosses/mini-bosses so they can actually detect the player from far away
            var elite = GetComponent<EliteEnemyAI>();
            if (elite != null)
            {
                if (elite.IsBoss)
                {
                    aggroRange = 14f;
                    verticalTolerance = 4f;
                    Debug.Log($"[GoblinAI] Scaled boss {gameObject.name} aggroRange to {aggroRange} and verticalTolerance to {verticalTolerance}");
                }
                else if (elite.IsMiniBoss)
                {
                    aggroRange = 8f;
                    verticalTolerance = 2.5f;
                    Debug.Log($"[GoblinAI] Scaled mini-boss {gameObject.name} aggroRange to {aggroRange} and verticalTolerance to {verticalTolerance}");
                }
            }
        }

        private void Start()
        {
            ApplyFacingVisual(); // align probes to the initial facing direction
            ChangeState(State.Patrol);
        }

        private void FixedUpdate()
        {
            _stateTimer += Time.fixedDeltaTime;

            switch (_state)
            {
                case State.Patrol:       TickPatrol();       break;
                case State.Anticipation: TickAnticipation(); break;
                case State.ActiveStrike: TickActiveStrike(); break;
                case State.Recovery:     TickRecovery();     break;
            }
        }

        // ---------------------------------------------------------------------
        // Centralised, de-coupled transition handling
        // ---------------------------------------------------------------------
        private void ChangeState(State next)
        {
            // ----- Exit logic for the state we are leaving -----
            switch (_state)
            {
                case State.ActiveStrike:
                    // Kill horizontal momentum so we don't slide into Recovery.
                    _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                    break;
            }

            _state = next;
            _stateTimer = 0f;

            // ----- Enter logic for the state we are entering -----
            switch (_state)
            {
                case State.Patrol:
                    if (_animator != null) _animator.Play(animPrefix + "_Walking");
                    break;

                case State.Anticipation:
                    // Freeze in place for the telegraph.
                    _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                    FaceTowardsPlayer();
                    if (_animator != null) _animator.Play(animPrefix + "_Idle");
                    break;

                case State.ActiveStrike:
                    // Lock the dash direction at the moment the strike begins.
                    _strikeDirection = new Vector2(_facingDir, 0f);

                    // Choose attack animation for Skeleton (cycle through 1 and 2)
                    string animName = animPrefix + "_Attack";
                    if (animPrefix == "Skeleton" && Random.value > 0.5f) animName = "Skeleton_Attack_2";

                    if (_animator != null) _animator.Play(animName);
                    break;

                case State.Recovery:
                    // Absolute dead-zone: no movement, fully vulnerable.
                    _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                    if (_animator != null) _animator.Play(animPrefix + "_Idle");
                    break;
            }
        }

        // ---------------------------------------------------------------------
        // State behaviours
        // ---------------------------------------------------------------------
        private void TickPatrol()
        {
            // Grid-limit awareness: turn around at ledges and walls.
            if (IsAtEdge() || IsBlockedByWall())
                Flip();

            _rb.linearVelocity = new Vector2(_facingDir * patrolSpeed, _rb.linearVelocity.y);

            // Engage the player only when within range and roughly on the same level.
            if (PlayerInAttackRange())
                ChangeState(State.Anticipation);
        }

        private void TickAnticipation()
        {
            // Hold completely still for the wind-up, keep facing the target.
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

            if (_stateTimer >= anticipationDuration)
                ChangeState(State.ActiveStrike);
        }

        private void TickActiveStrike()
        {
            // Regular strike velocity without complex deceleration
            _rb.linearVelocity = new Vector2(_strikeDirection.x * strikeSpeed, _rb.linearVelocity.y);

            // Bail early if we hit a wall so we don't grind into the tilemap.
            if (IsBlockedByWall() || _stateTimer >= _currentAttackDuration)
                ChangeState(State.Recovery);
        }

        private void TickRecovery()
        {
            // Dead-zone: stand still and remain vulnerable.
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

            if (_stateTimer >= recoveryDuration)
                ChangeState(State.Patrol);
        }

        // ---------------------------------------------------------------------
        // Perception helpers
        // ---------------------------------------------------------------------
        private bool PlayerInAttackRange()
        {
            if (player == null) return false;

            Vector2 delta = (Vector2)player.position - (Vector2)transform.position;
            if (Mathf.Abs(delta.y) > verticalTolerance) return false;
            
            // Aggro on player from any angle as long as they are within range
            return Mathf.Abs(delta.x) <= aggroRange;
        }

        /// <summary>True when the downward ray finds NO ground ahead (a ledge / gap).</summary>
        private bool IsAtEdge()
        {
            if (edgeCheck == null) return false;
            float scaledDistance = edgeCheckDistance * Mathf.Abs(transform.lossyScale.y);
            RaycastHit2D hit = Physics2D.Raycast(edgeCheck.position, Vector2.down, scaledDistance, groundLayer);
            return hit.collider == null;
        }

        /// <summary>True when the forward ray hits a solid tile (a wall).</summary>
        private bool IsBlockedByWall()
        {
            if (wallCheck == null) return false;
            Vector2 dir = new Vector2(_facingDir, 0f);
            float scaledDistance = wallCheckDistance * Mathf.Abs(transform.lossyScale.x);
            RaycastHit2D hit = Physics2D.Raycast(wallCheck.position, dir, scaledDistance, groundLayer);
            return hit.collider != null;
        }

        // ---------------------------------------------------------------------
        // Facing
        // ---------------------------------------------------------------------
        private void Flip()
        {
            _facingDir = -_facingDir;
            ApplyFacingVisual();
        }

        private void FaceTowardsPlayer()
        {
            if (player == null) return;
            float dx = player.position.x - transform.position.x;
            if (Mathf.Abs(dx) < 0.01f) return;

            int desired = dx > 0f ? 1 : -1;
            if (desired != _facingDir)
            {
                _facingDir = desired;
                ApplyFacingVisual();
            }
        }

        private void ApplyFacingVisual()
        {
            // Flip only the visual container, leaving the root transform (Rigidbody2D, colliders,
            // probes, health-bar canvas) completely untouched.
            Vector3 scale = _visualRoot.localScale;
            _visualRoot.localScale = new Vector3(Mathf.Abs(scale.x) * _facingDir, scale.y, scale.z);

            // When we flip the whole root, the probes are children of the root and are therefore
            // already mirrored by the scale flip — mirroring them again would cancel the flip out.
            // The health-bar canvas, however, must be counter-flipped so it never renders mirrored.
            if (_flipsRoot)
            {
                if (_uiCanvas != null)
                {
                    Vector3 c = _uiCanvas.localScale;
                    _uiCanvas.localScale = new Vector3(_facingDir * _uiCanvasBaseScaleX, c.y, c.z);
                }
                return;
            }

            // Mirror the edge/wall probes to the front of the goblin so ledge and wall
            // detection works while facing either direction (true edge-to-edge patrol).
            if (edgeCheck != null)
            {
                Vector3 e = edgeCheck.localPosition;
                edgeCheck.localPosition = new Vector3(Mathf.Abs(e.x) * _facingDir, e.y, e.z);
            }
            if (wallCheck != null)
            {
                Vector3 w = wallCheck.localPosition;
                wallCheck.localPosition = new Vector3(Mathf.Abs(w.x) * _facingDir, w.y, w.z);
            }
        }

        // ---------------------------------------------------------------------
        // Editor gizmos
        // ---------------------------------------------------------------------
        private void OnDrawGizmosSelected()
        {
            int dir = Application.isPlaying ? _facingDir : 1;

            if (edgeCheck != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(edgeCheck.position,
                    edgeCheck.position + Vector3.down * edgeCheckDistance);
            }

            if (wallCheck != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(wallCheck.position,
                    wallCheck.position + Vector3.right * dir * wallCheckDistance);
            }

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, aggroRange);
        }
    }
}
