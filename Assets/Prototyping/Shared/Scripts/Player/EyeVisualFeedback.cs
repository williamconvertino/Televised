using UnityEngine;

namespace Televised.Prototyping.Shared
{
    /// <summary>
    /// Cosmetic squash/stretch of the eyeball sprite: landing squash, velocity stretch while
    /// airborne, tiny idle breathing. Only scales the Eyeball visual; never touches the root.
    /// </summary>
    public class EyeVisualFeedback : MonoBehaviour
    {
        [SerializeField] PlayerMotor2D motor;
        [Tooltip("Transform that gets squashed (the white of the eye).")]
        [SerializeField] Transform eyeball;
        [SerializeField] float landingSquash = 0.22f;
        [SerializeField] float squashRecovery = 12f;
        [SerializeField] float airStretchPerSpeed = 0.012f;
        [SerializeField] float maxAirStretch = 0.15f;
        [SerializeField] float breathAmount = 0.02f;
        [SerializeField] float breathSpeed = 2.2f;

        float _squash;
        Vector3 _baseScale = Vector3.one;

        void Awake()
        {
            if (motor == null) motor = GetComponentInParent<PlayerMotor2D>();
            if (eyeball != null) _baseScale = eyeball.localScale;
        }

        void OnEnable() { if (motor != null) motor.Attached += OnAttached; }
        void OnDisable() { if (motor != null) motor.Attached -= OnAttached; }

        void OnAttached(Surface2D surface, bool transfer)
        {
            if (!transfer) _squash = landingSquash;
        }

        void LateUpdate()
        {
            if (motor == null || eyeball == null) return;
            _squash *= Mathf.Exp(-squashRecovery * Time.deltaTime);

            Vector2 axis;
            float along;
            if (motor.IsAttached)
            {
                axis = motor.SmoothedNormal;
                along = 1f - _squash + Mathf.Sin(Time.time * breathSpeed) * breathAmount;
            }
            else
            {
                Vector2 v = motor.Velocity;
                axis = v.sqrMagnitude > 1e-4f ? v.normalized : Vector2.up;
                along = 1f + Mathf.Min(maxAirStretch, v.magnitude * airStretchPerSpeed);
            }
            float across = 1f / Mathf.Max(0.1f, along); // preserve area

            eyeball.rotation = Quaternion.FromToRotation(Vector3.up, axis);
            eyeball.localScale = new Vector3(_baseScale.x * across, _baseScale.y * along, _baseScale.z);
        }
    }
}
