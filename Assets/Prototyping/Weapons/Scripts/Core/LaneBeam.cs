using System.Collections.Generic;
using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// A wide beam built from thin parallel lanes, each raycast against terrain on its own. Surfaces therefore cut
    /// off only the part of the beam they're in front of, and the rest continues (a pillar splits the beam in two).
    /// A lane only exists if the eye can see its start point, so the beam spreads out from the eye and never
    /// begins on the far side of a wall. Used by the eye beam and the holy beams.
    /// </summary>
    public class LaneBeam
    {
        public struct Lane
        {
            public Vector2 start;
            /// <summary>Signed offset from the beam's centre line.</summary>
            public float offset;
            public float length;
        }

        public readonly List<Lane> Lanes = new List<Lane>();
        public Vector2 Origin { get; private set; }
        public Vector2 Direction { get; private set; }
        public float Width { get; private set; }
        public float LaneWidth { get; private set; }
        public float Extent { get; private set; }

        readonly List<CombatQueries.LineHit> _laneHits = new List<CombatQueries.LineHit>();
        readonly Dictionary<DummyEnemy, CombatQueries.LineHit> _best = new Dictionary<DummyEnemy, CombatQueries.LineHit>();

        public void Compute(Vector2 origin, Vector2 dir, float width, float extent, bool blockedByTerrain, float laneSpacing)
        {
            Origin = origin;
            Direction = dir;
            Width = width;
            Extent = extent;
            Lanes.Clear();
            int count = Mathf.Clamp(Mathf.CeilToInt(width / Mathf.Max(0.02f, laneSpacing)), 1, 64);
            LaneWidth = width / count;
            Vector2 side = new Vector2(-dir.y, dir.x);
            for (int i = 0; i < count; i++)
            {
                float offset = -width * 0.5f + LaneWidth * (i + 0.5f);
                Vector2 start = origin + side * offset;
                float length = extent;
                if (blockedByTerrain)
                {
                    if (!CombatQueries.LineOfSight(origin, start)) length = 0f;
                    else if (CombatQueries.RaycastTerrain(start, dir, extent, out float d, out _)) length = d;
                }
                Lanes.Add(new Lane { start = start, offset = offset, length = length });
            }
        }

        /// <summary>Every enemy touching any lane, once each, sorted by distance along the beam.</summary>
        public void Hits(List<CombatQueries.LineHit> results)
        {
            _best.Clear();
            foreach (Lane lane in Lanes)
            {
                if (lane.length <= 0f) continue;
                CombatQueries.EnemiesAlongLine(lane.start, Direction, lane.length, LaneWidth * 0.5f, _laneHits);
                foreach (CombatQueries.LineHit h in _laneHits)
                    if (!_best.TryGetValue(h.enemy, out CombatQueries.LineHit b) || h.along < b.along) _best[h.enemy] = h;
            }
            results.Clear();
            results.AddRange(_best.Values);
            results.Sort((a, b) => a.along.CompareTo(b.along));
        }

        /// <summary>
        /// Draw the lanes. widthScale shrinks each lane (gaps open up as the beam dissolves); coreFraction draws a
        /// bright core over the lanes within that fraction of the half-width.
        /// </summary>
        public void Draw(Color color, RangeSettings fade, float widthScale = 1f, float coreFraction = 0f)
        {
            foreach (Lane lane in Lanes)
            {
                if (lane.length <= 0f) continue;
                CombatShapes.RangeBeam(lane.start, Direction, lane.length, LaneWidth * 1.04f * widthScale, color, fade);
                if (coreFraction > 0f && Mathf.Abs(lane.offset) <= Width * 0.5f * coreFraction)
                    CombatShapes.RangeBeam(lane.start, Direction, lane.length, LaneWidth * 1.04f * widthScale,
                        new Color(1f, 1f, 1f, 0.85f * color.a), fade);
            }
        }

        /// <summary>Thin guide lines along the outer edges and the centre (charge-up preview).</summary>
        public void DrawGuides(Color color, RangeSettings fade, float thickness)
        {
            if (Lanes.Count == 0) return;
            DrawGuide(Lanes[0]);
            DrawGuide(Lanes[Lanes.Count - 1]);
            DrawGuide(Lanes[Lanes.Count / 2]);

            void DrawGuide(Lane l)
            {
                if (l.length > 0f) CombatShapes.RangeBeam(l.start, Direction, l.length, thickness, color, fade);
            }
        }

        /// <summary>Lane outlines for the attack-geometry debug view.</summary>
        public void DrawDebug(Color c)
        {
            foreach (Lane lane in Lanes)
                if (lane.length > 0f)
                    DebugLines.Line(lane.start, lane.start + Direction * lane.length, c);
        }
    }
}
