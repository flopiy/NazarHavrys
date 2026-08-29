using UnityEngine;

namespace FungalCurse.Environment
{
    /// <summary>
    /// Smooth 2D camera follow. Runs in LateUpdate (after movement + before parallax reads the camera
    /// delta in its own LateUpdate ordering) which is the pixel-perfect-friendly tracking point.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector2 offset = new Vector2(0f, 1.5f);
        [SerializeField] private float smoothTime = 0.15f;
        [Tooltip("Optional world clamp for the camera centre. Leave max <= min to disable an axis clamp.")]
        [SerializeField] private Vector2 minBounds = Vector2.zero;
        [SerializeField] private Vector2 maxBounds = Vector2.zero;

        private Vector3 _velocity;

        private void Start()
        {
            if (target == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) target = tagged.transform;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);

            if (maxBounds.x > minBounds.x)
                desired.x = Mathf.Clamp(desired.x, minBounds.x, maxBounds.x);
            if (maxBounds.y > minBounds.y)
                desired.y = Mathf.Clamp(desired.y, minBounds.y, maxBounds.y);

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
        }
    }
}
