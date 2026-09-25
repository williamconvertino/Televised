using UnityEngine;
using UnityEngine.InputSystem;

namespace Televised.Prototyping.Shared
{
    /// <summary>Follow camera for the sandbox with scroll zoom and an overview toggle (C).</summary>
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(Camera))]
    public class SandboxCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] float followSharpness = 6f;
        [SerializeField] float size = 8f;
        [SerializeField] Vector2 sizeRange = new Vector2(3f, 25f);
        [SerializeField] float zoomStep = 1.1f;
        [SerializeField] Vector2 overviewCenter = new Vector2(0f, 2f);
        [SerializeField] float overviewSize = 18f;
        [SerializeField] bool overview;

        Camera _cam;

        void Awake() => _cam = GetComponent<Camera>();

        void LateUpdate()
        {
            if (PrototypeInput.Pressed(Key.C)) overview = !overview;

            float scroll = PrototypeInput.ScrollDelta;
            if (Mathf.Abs(scroll) > 0.01f)
                size = Mathf.Clamp(size * (scroll > 0f ? 1f / zoomStep : zoomStep), sizeRange.x, sizeRange.y);

            float k = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            Vector2 goal = overview || target == null ? overviewCenter : (Vector2)target.position;
            Vector2 p = Vector2.Lerp(transform.position, goal, k);
            transform.position = new Vector3(p.x, p.y, transform.position.z);
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, overview ? overviewSize : size, k);
        }
    }
}
