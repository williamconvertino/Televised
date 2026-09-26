using System;
using System.Collections.Generic;
using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// Weapon 10: an offensive chain.
    ///   Press:   the chain shoots toward the cursor and skewers the enemies it passes (outgoing damage).
    ///   Hold:    it becomes a loose, heavy rope (verlet) tethering the skewered enemies. Moving, jumping and the
    ///            cursor tug swing them around, and slamming them into terrain deals wall-slam damage.
    ///   Release: it reels in fast, dragging them to the eye, then flings them past it (retract damage on release).
    /// Separate from the movement grapple: it never moves the player.
    /// </summary>
    public class ChainWeapon : PrototypeWeapon
    {
        [Serializable]
        public class ChainSettings
        {
            [Min(0f)] public float outgoingDamage = 15f;
            [Tooltip("Damage when a skewered enemy is released at the eye (0 = none).")]
            [Min(0f)] public float retractDamage = 10f;
            public RangeSettings range = new RangeSettings(11f, 0.8f);
            [Min(1f)] public float extensionSpeed = 55f;
            [Tooltip("How fast the chain reels in after you release (the pull).")]
            [Min(1f)] public float retractionSpeed = 40f;
            [Min(0.02f)] public float chainWidth = 0.45f;
            [Tooltip("0 = unlimited.")]
            [Min(0)] public int maxSkeweredEnemies;
            [Tooltip("Stop extending as soon as the chain is full.")]
            public bool stopWhenFull = true;
            [Tooltip("Terrain stops the chain's extension.")]
            public bool stopOnTerrain = true;

            [Header("Rope (while held)")]
            [Range(4, 32)] public int links = 16;
            [Tooltip("Gravity on the chain and the enemies on it.")]
            [Min(0f)] public float chainGravity = 14f;
            [Tooltip("Velocity lost per second (air drag).")]
            [Min(0f)] public float chainDamping = 0.6f;
            [Tooltip("How much heavier a link gets per unit of skewered enemy mass (heavier = more swing momentum).")]
            [Min(0f)] public float enemyWeight = 2f;
            [Tooltip("While held, the chain's tip is pulled toward the cursor (0 = only your movement swings it).")]
            [Min(0f)] public float cursorTug = 45f;
            [Tooltip("Seconds you can hold before it reels in by itself (0 = unlimited).")]
            [Min(0f)] public float maxHoldTime;

            [Header("Release")]
            [Tooltip("Fraction of their pull velocity enemies keep when released (flung past / into things).")]
            [Range(0f, 1.5f)] public float releaseVelocity = 0.8f;
            [Tooltip("Enemies are released this far from the eye's edge.")]
            [Min(0f)] public float releaseDistance = 0.4f;
            [Min(0f)] public float cooldown = 0.3f;

            public PierceSettings pierce = new PierceSettings(true, 0, PierceFalloffMode.Multiplicative, 0.85f);
            public KnockbackSettings releaseKnockback = new KnockbackSettings(0f, KnockbackDirection.AwayFromSource);
            public Color color = new Color(0.8f, 0.85f, 0.9f, 1f);
        }

        [SerializeField] ChainSettings settings = new ChainSettings();

        public override string DisplayName => "Chain / Skewer";
        public override string Summary => "Skewer enemies, hold to swing them on a loose chain (slam them into walls), release to reel in.";
        public override object Settings => settings;

        enum State { Idle, Extending, Tethered, Retracting }

        class Skewered
        {
            public DummyEnemy enemy;
            public float along;
            public int node;
        }

        State _state;
        bool _held;
        Vector2 _dir;
        float _length, _holdTime;
        HitTracker _tracker = new HitTracker();
        readonly List<Skewered> _skewered = new List<Skewered>();
        readonly List<CombatQueries.LineHit> _hits = new List<CombatQueries.LineHit>();

        // Rope nodes: 0 = player root, last = tip.
        Vector2[] _p = new Vector2[0], _prev = new Vector2[0];
        float[] _invMass = new float[0];
        readonly List<Vector2> _drawPts = new List<Vector2>();

        public override WeaponPhase Phase => _state == State.Idle ? (CooldownReady ? WeaponPhase.Ready : WeaponPhase.CoolingDown) : WeaponPhase.Active;
        public override float PhaseProgress => _state == State.Tethered && settings.maxHoldTime > 0f
            ? 1f - Mathf.Clamp01(_holdTime / settings.maxHoldTime)
            : _state == State.Retracting && _p.Length > 0 ? Mathf.Clamp01(_length / Mathf.Max(0.01f, settings.range.maxRange))
            : base.PhaseProgress;
        public override string HudExtra => _state != State.Idle ? $"{_state} ({_skewered.Count} skewered)" : null;

        public override void BeginFire()
        {
            _held = true;
            if (_state != State.Idle || !CooldownReady) return;
            CountActivation();
            _state = State.Extending;
            _dir = C.AimDirection;
            _length = 0f;
            _holdTime = 0f;
            _tracker = new HitTracker();
            _skewered.Clear();
        }

        public override void EndFire()
        {
            _held = false;
            if (_state == State.Tethered) _state = State.Retracting;
        }

        public override void Cancel()
        {
            _held = false;
            if (_state == State.Idle) return;
            foreach (Skewered k in _skewered)
                if (k.enemy != null) k.enemy.ReleaseControl(this, Vector2.zero);
            _skewered.Clear();
            _state = State.Idle;
            StartCooldown(settings.cooldown);
        }

        public override void Tick(float dt, bool selected)
        {
            if (_state == State.Idle || dt <= 0f) return;
            _skewered.RemoveAll(k => k.enemy == null || !k.enemy.IsAlive);

            if (_state == State.Extending) Extend(dt);
            else SimulateRope(dt);
        }

        // ---------------------------------------------------------------- extension

        void Extend(float dt)
        {
            ChainSettings s = settings;
            float from = _length;
            float to = Mathf.Min(s.range.maxRange, _length + s.extensionSpeed * dt);
            bool stop = to >= s.range.maxRange;
            Vector2 origin = C.Origin;
            if (s.stopOnTerrain && CombatQueries.RaycastTerrain(origin, _dir, to, out float t, out _))
            {
                to = Mathf.Max(from, t);
                stop = true;
            }
            _length = to;

            CombatQueries.EnemiesAlongLine(origin, _dir, to, s.chainWidth * 0.5f, _hits);
            int max = Mathf.Min(s.maxSkeweredEnemies <= 0 ? int.MaxValue : s.maxSkeweredEnemies, s.pierce.MaxTargets);
            foreach (CombatQueries.LineHit h in _hits)
            {
                if (_skewered.Count >= max) break;
                if (_tracker.HasHit(h.enemy) || !h.enemy.TryTakeControl(this)) continue;
                _tracker.Record(h.enemy, -1f);
                var ev = DamageEvent.Weapon(DisplayName, this, s.outgoingDamage, h.along, h.along / s.range.maxRange,
                    null, _skewered.Count, s.pierce);
                ev.sourcePosition = origin;
                ev.hitPoint = h.point;
                ev.hitDirection = _dir;
                _skewered.Add(new Skewered { enemy = h.enemy, along = h.along });
                CombatDamage.Apply(h.enemy, ref ev);
            }
            if (s.stopWhenFull && _skewered.Count >= max) stop = true;

            // Skewered enemies ride the straight chain while it's going out.
            foreach (Skewered k in _skewered)
                k.enemy.SetControlledPosition(this, origin + _dir * Mathf.Min(k.along, _length), dt, out _);

            if (stop) BeginRope();
        }

        void BeginRope()
        {
            ChainSettings s = settings;
            int n = s.links + 1;
            _p = new Vector2[n];
            _prev = new Vector2[n];
            _invMass = new float[n];
            Vector2 origin = C.Origin;
            float seg = Mathf.Max(0.01f, _length / s.links);
            for (int i = 0; i < n; i++)
            {
                _p[i] = _prev[i] = origin + _dir * (seg * i);
                _invMass[i] = i == 0 ? 0f : 1f;
            }
            foreach (Skewered k in _skewered)
            {
                k.node = Mathf.Clamp(Mathf.RoundToInt(k.along / seg), 1, n - 1);
                _invMass[k.node] = 1f / (1f + s.enemyWeight * k.enemy.Mass);
            }
            _state = _held ? State.Tethered : State.Retracting;
        }

        // ---------------------------------------------------------------- rope

        void SimulateRope(float dt)
        {
            ChainSettings s = settings;
            if (_state == State.Tethered)
            {
                _holdTime += dt;
                if (s.maxHoldTime > 0f && _holdTime >= s.maxHoldTime) _state = State.Retracting;
            }
            if (_state == State.Retracting) _length = Mathf.Max(0f, _length - s.retractionSpeed * dt);

            int n = _p.Length;
            float seg = Mathf.Max(0.001f, _length / (n - 1));
            const int substeps = 3;
            float h = Mathf.Min(dt, 1f / 20f) / substeps;
            float drag = Mathf.Exp(-s.chainDamping * h);
            Vector2 anchor = C.Origin;

            for (int step = 0; step < substeps; step++)
            {
                for (int i = 1; i < n; i++)
                {
                    Vector2 v = (_p[i] - _prev[i]) * drag;
                    _prev[i] = _p[i];
                    _p[i] += v + Vector2.down * (s.chainGravity * h * h);
                }
                if (_state == State.Tethered && s.cursorTug > 0f)
                {
                    Vector2 toCursor = C.CursorWorld - _p[n - 1];
                    if (toCursor.sqrMagnitude > 0.01f) _p[n - 1] += toCursor.normalized * (s.cursorTug * _invMass[n - 1] * h * h);
                }
                _p[0] = _prev[0] = anchor;

                // Links only resist stretching, so the chain hangs and whips loosely.
                for (int it = 0; it < 10; it++)
                {
                    for (int i = 0; i < n - 1; i++)
                    {
                        Vector2 d = _p[i + 1] - _p[i];
                        float len = d.magnitude;
                        if (len <= seg || len < 1e-6f) continue;
                        float wSum = _invMass[i] + _invMass[i + 1];
                        if (wSum <= 0f) continue;
                        Vector2 corr = d * ((len - seg) / len / wSum);
                        _p[i] += corr * _invMass[i];
                        _p[i + 1] -= corr * _invMass[i + 1];
                    }
                    _p[0] = anchor;
                }

                // Enemies ride their links, collide with terrain (slams) and push their link back out.
                foreach (Skewered k in _skewered)
                {
                    int i = k.node;
                    _p[i] = k.enemy.SetControlledPosition(this, _p[i], h, out Vector2 pushN);
                    if (pushN == Vector2.zero) continue;
                    Vector2 vel = _p[i] - _prev[i];
                    float vn = Vector2.Dot(vel, pushN);
                    if (vn < 0f) _prev[i] = _p[i] - (vel - pushN * (vn * 1.4f));
                }
            }

            if (_state != State.Retracting) return;

            // Release enemies as they arrive at the eye, flinging them with their pull velocity.
            float releaseAt = C.Motor.Radius + s.releaseDistance;
            for (int k = _skewered.Count - 1; k >= 0; k--)
            {
                Skewered sk = _skewered[k];
                if ((sk.enemy.Position - anchor).magnitude - sk.enemy.Radius > releaseAt && _length > releaseAt) continue;
                Vector2 vel = (_p[sk.node] - _prev[sk.node]) / h;
                Release(sk, vel * s.releaseVelocity);
                _skewered.RemoveAt(k);
            }
            if (_length <= releaseAt)
            {
                _state = State.Idle;
                StartCooldown(s.cooldown);
            }
        }

        void Release(Skewered k, Vector2 velocity)
        {
            ChainSettings s = settings;
            k.enemy.ReleaseControl(this, velocity, DisplayName);
            k.enemy.Contact.Suppress(0.5f); // flung past the player: don't let it hurt you on the way
            if (s.retractDamage <= 0f && s.releaseKnockback.force <= 0f) return;
            Vector2 origin = C.Origin;
            float dist = (k.enemy.Position - origin).magnitude;
            var ev = DamageEvent.Weapon(DisplayName, this, s.retractDamage, dist, 0f, null, 0, null);
            ev.sourcePosition = origin;
            ev.hitPoint = k.enemy.Position;
            ev.hitDirection = velocity.sqrMagnitude > 1e-6f ? velocity.normalized : -_dir;
            ev.knockback = s.releaseKnockback.Compute(origin, ev.hitDirection, origin, k.enemy.Position, 1f);
            if (s.retractDamage > 0f) CombatDamage.Apply(k.enemy, ref ev);
            else k.enemy.AddKnockback(ev.knockback, DisplayName);
        }

        // ---------------------------------------------------------------- drawing

        void LateUpdate()
        {
            if (C == null || _state == State.Idle) return;
            ChainSettings s = settings;
            _drawPts.Clear();
            if (_state == State.Extending)
            {
                _drawPts.Add(C.Origin);
                _drawPts.Add(C.Origin + _dir * _length);
            }
            else
            {
                _drawPts.AddRange(_p);
                _drawPts[0] = C.Origin;
            }

            // Links every ~0.28 units along the path, fading with distance traveled along the chain.
            const float spacing = 0.28f;
            float traveled = 0f, nextLink = 0.3f;
            for (int i = 0; i + 1 < _drawPts.Count; i++)
            {
                Vector2 a = _drawPts[i], b = _drawPts[i + 1];
                float len = (b - a).magnitude;
                if (len < 1e-5f) continue;
                Vector2 d = (b - a) / len;
                Vector2 side = new Vector2(-d.y, d.x);
                CombatShapes.Line(a, b, s.chainWidth * 0.2f, CombatShapes.WithAlpha(s.color, 0.6f * s.range.Fade(traveled)));
                while (nextLink <= traveled + len)
                {
                    Vector2 p = a + d * (nextLink - traveled);
                    Color c = CombatShapes.WithAlpha(s.color, s.color.a * s.range.Fade(nextLink));
                    float w = s.chainWidth * 0.3f;
                    CombatShapes.Quad(p - d * 0.12f, p + side * w, p + d * 0.12f, p - side * w, c, c, c, c);
                    nextLink += spacing;
                }
                traveled += len;
            }

            // Barbed tip.
            Vector2 tip = _drawPts[_drawPts.Count - 1];
            Vector2 td = _drawPts.Count >= 2 ? (tip - _drawPts[_drawPts.Count - 2]) : _dir;
            td = td.sqrMagnitude > 1e-8f ? td.normalized : _dir;
            Vector2 ts = new Vector2(-td.y, td.x);
            Color tc = new Color(1f, 0.85f, 0.5f, 1f);
            CombatShapes.Triangle(tip + td * 0.35f, tip + ts * s.chainWidth, tip - ts * s.chainWidth, tc, tc, tc);
        }

        public override void DrawDebug()
        {
            Vector2 o = C.Origin;
            Vector2 dir = _state == State.Idle ? C.AimDirection : _dir;
            if (CombatDebug.WeaponRange) DebugLines.Line(o, o + dir * settings.range.maxRange, CombatDebug.RangeColor);
            if (CombatDebug.AttackGeometry && _state == State.Extending)
                CombatDebug.Capsule(o, o + dir * _length, settings.chainWidth * 0.5f, CombatDebug.GeometryColor);
            if (CombatDebug.AttackGeometry && (_state == State.Tethered || _state == State.Retracting))
                foreach (Skewered k in _skewered) DebugLines.Circle(k.enemy.Position, k.enemy.Radius + 0.1f, CombatDebug.GeometryColor, 16);
        }

        public override void DrawTuning(TuningGui g)
        {
            ChainSettings s = settings;
            g.Info("Press: shoot + skewer.  Hold: swing them on the chain.  Release: reel in and fling.");
            g.Header("Damage");
            s.outgoingDamage = g.Slider("Outgoing Damage (skewer)", s.outgoingDamage, 0f, 100f, "0");
            s.retractDamage = g.Slider("Retract Damage (on release)", s.retractDamage, 0f, 100f, "0");
            s.cooldown = g.Slider("Cooldown", s.cooldown, 0f, 3f);
            g.Info("Slamming skewered enemies into terrain deals wall-slam damage (IMPACT tab).");
            g.Header("Throw");
            s.extensionSpeed = g.Slider("Extension Speed", s.extensionSpeed, 5f, 120f, "0");
            s.chainWidth = g.Slider("Chain Width (skewer)", s.chainWidth, 0.05f, 1.5f);
            s.maxSkeweredEnemies = g.IntSlider("Max Skewered (0 = unlimited)", s.maxSkeweredEnemies, 0, 10);
            s.stopWhenFull = g.Toggle("Stop when full", s.stopWhenFull);
            s.stopOnTerrain = g.Toggle("Terrain stops the throw", s.stopOnTerrain);
            g.Header("Rope (hold)");
            s.links = g.IntSlider("Links", s.links, 4, 32);
            s.chainGravity = g.Slider("Chain Gravity", s.chainGravity, 0f, 40f, "0.0");
            s.chainDamping = g.Slider("Damping", s.chainDamping, 0f, 5f);
            s.enemyWeight = g.Slider("Enemy Weight On Chain", s.enemyWeight, 0f, 6f);
            s.cursorTug = g.Slider("Cursor Tug", s.cursorTug, 0f, 150f, "0");
            s.maxHoldTime = g.Slider("Max Hold Time (0 = unlimited)", s.maxHoldTime, 0f, 6f);
            g.Header("Pull (release)");
            s.retractionSpeed = g.Slider("Pull Speed", s.retractionSpeed, 5f, 120f, "0");
            s.releaseVelocity = g.Slider("Fling (velocity kept)", s.releaseVelocity, 0f, 1.5f);
            s.releaseDistance = g.Slider("Release Distance", s.releaseDistance, 0f, 3f);
            g.Header("Range");
            g.Range(s.range, 30f);
            g.Header("Piercing");
            g.Pierce(s.pierce);
            g.Header("Release Knockback");
            g.Knockback(s.releaseKnockback, 30f);
        }
    }
}
