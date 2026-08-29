using System;
using UnityEngine;

namespace FungalCurse.Environment
{
    /// <summary>
    /// "The Midnight Bramble" — 3-tier parallax background driver.
    ///
    /// Each layer moves a fraction of the camera's movement. A multiplier of 1 makes a layer appear
    /// infinitely far (locked to the camera, i.e. visually static, used for the sky/star field). Lower
    /// multipliers (0.85 distant forest, 0.5 near foliage) scroll progressively faster relative to the
    /// sky, producing depth. Runs in LateUpdate so it tracks the camera after it has moved this frame
    /// (also the pixel-perfect-friendly ordering).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParallaxController : MonoBehaviour
    {
        [Serializable]
        public sealed class Layer
        {
            public Transform target;
            [Range(0f, 1f)]
            [Tooltip("0 = world-locked, 1 = camera-locked (appears infinitely distant).")]
            public float multiplier = 0.5f;
            [Tooltip("If true, only horizontal parallax is applied (vertical stays fixed).")]
            public bool horizontalOnly = true;

            [HideInInspector] public Vector3 startPos;
        }

        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Layer[] layers;

        private Vector3 _prevCamPos;

        private void Start()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            _prevCamPos = cameraTransform != null ? cameraTransform.position : Vector3.zero;

            if (layers != null)
                foreach (var l in layers)
                    if (l.target != null) l.startPos = l.target.position;
        }

        private void LateUpdate()
        {
            if (cameraTransform == null || layers == null) return;

            Vector3 camPos = cameraTransform.position;
            Vector3 delta = camPos - _prevCamPos;

            foreach (var l in layers)
            {
                if (l.target == null) continue;
                float dx = delta.x * l.multiplier;
                float dy = l.horizontalOnly ? 0f : delta.y * l.multiplier;
                l.target.position += new Vector3(dx, dy, 0f);
            }

            _prevCamPos = camPos;
        }
    }
}
