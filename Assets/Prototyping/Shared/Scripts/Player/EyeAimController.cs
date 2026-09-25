using UnityEngine;

namespace Televised.Prototyping.Shared
{
    /// <summary>
    /// Looks the iris/pupil toward the mouse. Purely visual; has no influence on the player's
    /// physical orientation. <see cref="AimDirection"/> is exposed for future systems
    /// (projectiles, gaze attacks, cursor-directed jumping, targeting).
    /// </summary>
    public class EyeAimController : MonoBehaviour
    {
        [Tooltip("The player's true root. Aim is measured from here.")]
        [SerializeField] Transform root;
        [SerializeField] Camera aimCamera;
        [SerializeField] Transform iris;
        [SerializeField] Transform pupil;
        [Tooltip("Max world offset of the iris from the eye center.")]
        [SerializeField] float irisOffset = 0.2f;
        [Tooltip("Pupil offset (slightly more than iris for a bit of depth).")]
        [SerializeField] float pupilOffset = 0.25f;
        [Tooltip("Cursor distance at which the offset reaches its maximum.")]
        [SerializeField] float fullOffsetDistance = 2f;
        [Tooltip("Aim follow sharpness (1/s). 0 = instant.")]
        [SerializeField] float sharpness = 25f;

        public Vector2 AimDirection { get; private set; } = Vector2.right;
        public Vector2 AimWorldPoint { get; private set; }

        Vector2 _currentOffset01;

        void LateUpdate()
        {
            if (root == null) root = transform.parent != null ? transform.parent : transform;
            if (aimCamera == null) aimCamera = Camera.main;

            Vector2 origin = root.position;
            AimWorldPoint = PrototypeInput.MouseWorld(aimCamera);
            Vector2 to = AimWorldPoint - origin;
            if (to.sqrMagnitude > 1e-6f) AimDirection = to.normalized;

            Vector2 target = AimDirection * Mathf.Clamp01(to.magnitude / Mathf.Max(0.01f, fullOffsetDistance));
            _currentOffset01 = sharpness <= 0f
                ? target
                : Vector2.Lerp(_currentOffset01, target, 1f - Mathf.Exp(-sharpness * Time.deltaTime));

            Vector2 center = transform.position;
            if (iris != null) SetWorld(iris, center + _currentOffset01 * irisOffset);
            if (pupil != null) SetWorld(pupil, center + _currentOffset01 * pupilOffset);
        }

        static void SetWorld(Transform t, Vector2 p) => t.position = new Vector3(p.x, p.y, t.position.z);
    }
}
