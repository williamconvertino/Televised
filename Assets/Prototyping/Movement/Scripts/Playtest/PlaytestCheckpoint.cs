using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Movement.Playtest
{
    /// <summary>
    /// A checkpoint (or the goal). Touching it (player center within <see cref="radius"/>) activates it.
    /// Checkpoints are ordered by <see cref="index"/>; the session respawns the player at the highest one reached.
    /// </summary>
    public class PlaytestCheckpoint : MonoBehaviour
    {
        public int index;
        public bool isGoal;
        [Min(0.1f)] public float radius = 1.4f;
        [SerializeField] SpriteRenderer ring;
        [SerializeField] SpriteRenderer core;

        static readonly Color InactiveRing = new Color(0.6f, 0.7f, 0.9f, 0.35f);
        static readonly Color InactiveCore = new Color(0.6f, 0.7f, 0.9f, 0.9f);
        static readonly Color ReachedRing = new Color(0.3f, 1f, 0.45f, 0.35f);
        static readonly Color ReachedCore = new Color(0.3f, 1f, 0.45f, 1f);
        static readonly Color GoalRing = new Color(1f, 0.85f, 0.2f, 0.45f);
        static readonly Color GoalCore = new Color(1f, 0.85f, 0.2f, 1f);

        public Vector2 Position => transform.position;
        public bool Reached { get; private set; }

        void Start() => SetReached(false);

        public bool Contains(Vector2 p) => (p - Position).sqrMagnitude <= radius * radius;

        public void SetReached(bool reached)
        {
            Reached = reached;
            if (ring != null) ring.color = isGoal ? GoalRing : reached ? ReachedRing : InactiveRing;
            if (core != null) core.color = isGoal ? GoalCore : reached ? ReachedCore : InactiveCore;
        }

        void Update()
        {
            // Gentle pulse so checkpoints/goal read as interactive.
            if (ring == null) return;
            float s = radius * 2f * (1f + Mathf.Sin(Time.time * (isGoal ? 4f : 2.5f)) * 0.06f);
            ring.transform.localScale = new Vector3(s, s, 1f);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = isGoal ? Color.yellow : Color.green;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
