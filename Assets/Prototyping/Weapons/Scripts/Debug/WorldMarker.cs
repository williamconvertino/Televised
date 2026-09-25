using System.Collections.Generic;
using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// A world-space text label (zone names, distance markers) with an optional tick line below it.
    /// Drawn by <see cref="CombatWorldUI"/> while Zone Labels is on. Not a surface: the player can't touch it.
    /// </summary>
    public class WorldMarker : MonoBehaviour
    {
        public static readonly List<WorldMarker> All = new List<WorldMarker>();

        [TextArea] public string text = "Marker";
        public Color color = new Color(1f, 1f, 1f, 0.6f);
        [Min(6)] public int fontSize = 13;
        public bool bold;
        [Tooltip("Length of a line drawn downward from the label (0 = none).")]
        [Min(0f)] public float tickLength;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            if (tickLength <= 0f || !CombatDebug.ZoneLabels) return;
            Vector2 p = transform.position;
            DebugLines.Line(p + Vector2.down * 0.35f, p + Vector2.down * (0.35f + tickLength), color);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = color;
            Gizmos.DrawWireCube(transform.position, new Vector3(0.3f, 0.3f, 0f));
            if (tickLength > 0f) Gizmos.DrawLine(transform.position, transform.position + Vector3.down * (0.35f + tickLength));
        }
    }
}
