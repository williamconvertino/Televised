using UnityEngine;

namespace Televised.Prototyping.Movement.Playtest
{
    /// <summary>
    /// Moves a surface (platform, stepping stone, hazard) on a smooth ping-pong path and/or spins it.
    /// Motion is a pure function of time, so it stays in sync however often courses are restarted.
    /// Surface2D picks up transform changes automatically, and an attached player is carried along
    /// (PlayerMotor2D resamples the surface every frame and inherits its velocity when jumping off).
    /// </summary>
    [DefaultExecutionOrder(-200)] // before the motor, so it samples this frame's surface position
    public class PlaytestMover : MonoBehaviour
    {
        [Tooltip("World offset of the far end of the ping-pong path (zero = no translation).")]
        public Vector2 offset;
        [Tooltip("Seconds for a full there-and-back cycle.")]
        [Min(0.1f)] public float period = 4f;
        [Tooltip("Cycle phase in [0,1): staggers several movers.")]
        [Range(0f, 1f)] public float phase;
        [Tooltip("Degrees per second (counter-clockwise). 0 = no rotation.")]
        public float rotationSpeed;

        Vector3 _startPos;
        Quaternion _startRot;

        void Awake()
        {
            _startPos = transform.position;
            _startRot = transform.rotation;
        }

        void Update()
        {
            float t = Time.time;
            if (offset != Vector2.zero)
            {
                float k = 0.5f - 0.5f * Mathf.Cos((t / period + phase) * Mathf.PI * 2f);
                transform.position = _startPos + (Vector3)(offset * k);
            }
            if (rotationSpeed != 0f)
                transform.rotation = _startRot * Quaternion.Euler(0f, 0f, rotationSpeed * t + phase * 360f);
        }

        void OnDrawGizmosSelected()
        {
            if (offset == Vector2.zero) return;
            Vector3 a = Application.isPlaying ? _startPos : transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(a, a + (Vector3)offset);
        }
    }
}
