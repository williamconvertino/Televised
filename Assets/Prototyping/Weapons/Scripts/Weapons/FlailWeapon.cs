using System;
using System.Collections.Generic;
using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// Weapon 8: a weight on a rope tied to the player root. It's driven by the player's movement (jumping, falling,
    /// crawling round curves, grappling); damage scales with the head's speed into the enemy.
    /// Physics is a simple verlet-free point mass with a rope (or rod) constraint, sub-stepped. Arcade Assistance
    /// keeps it orbiting instead of hanging inert; fire whips it toward the cursor, holding steers it.
    /// The flail doesn't pull the player.
    /// </summary>
    public class FlailWeapon : PrototypeWeapon
    {
        [Serializable]
        public class FlailSettings
        {
            [Min(0.2f)] public float ropeLength = 3f;
            [Tooltip("Rod: the head is held at exactly rope length. Rope: it can go slack.")]
            public bool rigidRod;
            [Min(0.05f)] public float flailRadius = 0.6f;
            [Tooltip("Heavier = steering/assist accelerate it less, it keeps going through hits and knocks enemies harder.")]
            [Min(0.1f)] public float flailMass = 1.5f;
            [Min(0f)] public float gravity = 16f;
            [Tooltip("Fraction of the rope overstretch corrected per sub-step (1 = hard constraint).")]
            [Range(0.05f, 1f)] public float constraintStrength = 1f;
            [Tooltip("Velocity decay per second (air drag).")]
            [Min(0f)] public float damping = 0.4f;

            [Header("Arcade assistance")]
            [Tooltip("0 = pure physics. Higher keeps the flail orbiting and lighter.")]
            [Range(0f, 1f)] public float arcadeAssistance = 0.35f;
            [Tooltip("Orbit speed (relative to the player) the assistance tries to maintain.")]
            [Min(0f)] public float assistOrbitSpeed = 7f;
            [Min(0f)] public float assistAcceleration = 30f;

            [Header("Player control")]
            [Tooltip("Clicking sends the head toward the cursor at this speed (relative to the player).")]
            [Min(0f)] public float whipSpeed = 17f;
            [Tooltip("On: a click replaces the head's velocity, so it always goes where you click. Off: it's added.")]
            public bool whipReplacesVelocity = true;
            [Min(0f)] public float whipCooldown;
            [Tooltip("Holding fire keeps pulling the head toward the cursor (clamped to rope reach).")]
            [Min(0f)] public float holdSteerAcceleration = 35f;

            [Header("Terrain")]
            public bool collideWithTerrain = true;
            [Range(0f, 1f)] public float terrainBounce = 0.35f;

            [Header("Damage")]
            [Min(0f)] public float baseDamage = 12f;
            [Tooltip("Below this impact speed the head does no damage at all.")]
            [Min(0f)] public float minimumDamageSpeed = 4f;
            [Tooltip("Extra damage per unit of speed above the minimum.")]
            [Min(0f)] public float velocityDamageScale = 1f;
            [Min(0f)] public float maximumDamage = 40f;
            [Min(0f)] public float perTargetHitCooldown = 0.35f;
            [Tooltip("How much the head bounces off an enemy it hits (divided by mass).")]
            [Range(0f, 1f)] public float hitBounce = 0.5f;
            public KnockbackSettings knockback = new KnockbackSettings(6f, KnockbackDirection.AlongAttackDirection);
            [Tooltip("Extra knockback per unit of impact speed (times mass).")]
            [Min(0f)] public float knockbackSpeedScale = 0.35f;

            public Color ropeColor = new Color(0.75f, 0.7f, 0.6f, 0.9f);
            public Color headColor = new Color(0.62f, 0.66f, 0.75f, 1f);
            public Color hotColor = new Color(1f, 0.45f, 0.25f, 1f);
        }

        [SerializeField] FlailSettings settings = new FlailSettings();

        public override string DisplayName => "Flail";
        public override string Summary => "Weight on a rope, swung by your movement. Click to send it at the cursor, hold to steer.";
        public override object Settings => settings;
        public override WeaponPhase Phase => _deployed ? (CooldownReady ? WeaponPhase.Active : WeaponPhase.CoolingDown) : WeaponPhase.Ready;
        public override float PhaseRemaining => 0f;
        public override string HudExtra => _deployed ? $"Head speed {_headVel.magnitude:0.0}" : null;

        bool _deployed;
        bool _steering;
        Vector2 _headPos, _headVel;
        HitTracker _tracker = new HitTracker();
        readonly List<DummyEnemy> _touching = new List<DummyEnemy>();
        readonly List<Vector2> _ropePts = new List<Vector2>();
        readonly List<Color> _ropeCols = new List<Color>();

        public override void OnSelected() => Deploy();

        public override void OnDeselected() => Cancel();

        public override void Cancel()
        {
            _deployed = false;
            _steering = false;
        }

        void Deploy()
        {
            _deployed = true;
            _tracker = new HitTracker();
            _headPos = C.Origin + Vector2.down * settings.ropeLength * 0.9f;
            _headVel = C.Motor.Velocity;
            CountActivation();
        }

        public override void BeginFire()
        {
            if (!_deployed) Deploy();
            _steering = true;
            if (!CooldownReady || settings.whipSpeed <= 0f) return;
            Vector2 toCursor = C.CursorWorld - _headPos;
            if (toCursor.sqrMagnitude < 1e-6f) return;
            Vector2 fling = toCursor.normalized * settings.whipSpeed;
            _headVel = settings.whipReplacesVelocity ? C.Motor.Velocity + fling : _headVel + fling;
            StartCooldown(settings.whipCooldown);
        }

        public override void EndFire() => _steering = false;

        public override void Tick(float dt, bool selected)
        {
            if (!_deployed || !selected || dt <= 0f) return;
            FlailSettings s = settings;
            Vector2 anchor = C.Origin;
            Vector2 anchorVel = C.Motor.Velocity;
            // Teleports / respawns: bring the head along instead of yanking it across the map.
            if ((_headPos - anchor).sqrMagnitude > s.ropeLength * s.ropeLength * 16f)
            {
                _headPos = anchor + Vector2.down * s.ropeLength * 0.9f;
                _headVel = anchorVel;
            }
            const int steps = 4;
            float h = Mathf.Min(dt, 1f / 20f) / steps;

            for (int i = 0; i < steps; i++)
            {
                Vector2 accel = Vector2.down * (s.gravity * (1f - 0.6f * s.arcadeAssistance));

                Vector2 rel = _headPos - anchor;
                float dist = rel.magnitude;
                Vector2 radial = dist > 1e-4f ? rel / dist : Vector2.down;
                Vector2 tangent = new Vector2(-radial.y, radial.x);

                if (s.arcadeAssistance > 0f && dist > s.ropeLength * 0.5f)
                {
                    // Keep it orbiting in whichever direction it's already going.
                    float orbit = Vector2.Dot(_headVel - anchorVel, tangent);
                    float target = s.assistOrbitSpeed * s.arcadeAssistance;
                    if (Mathf.Abs(orbit) < target)
                        accel += tangent * (Mathf.Sign(orbit == 0f ? 1f : orbit) * s.assistAcceleration * s.arcadeAssistance / s.flailMass);
                }

                if (_steering && s.holdSteerAcceleration > 0f)
                {
                    Vector2 goal = anchor + Vector2.ClampMagnitude(C.CursorWorld - anchor, s.ropeLength);
                    Vector2 toGoal = goal - _headPos;
                    if (toGoal.sqrMagnitude > 1e-4f) accel += toGoal.normalized * (s.holdSteerAcceleration / s.flailMass);
                }

                _headVel += accel * h;
                _headVel *= Mathf.Exp(-s.damping * h);
                _headPos += _headVel * h;

                // Rope / rod constraint against the (kinematic) player.
                rel = _headPos - anchor;
                dist = rel.magnitude;
                if (dist > 1e-4f && (dist > s.ropeLength || s.rigidRod))
                {
                    radial = rel / dist;
                    float error = dist - s.ropeLength;
                    _headPos -= radial * (error * s.constraintStrength);
                    // Remove the radial velocity (relative to the player) that stretches the rope.
                    float vr = Vector2.Dot(_headVel - anchorVel, radial);
                    if (vr > 0f || s.rigidRod) _headVel -= radial * (vr * s.constraintStrength);
                }

                if (s.collideWithTerrain)
                {
                    Vector2 n = CombatQueries.PushOutOfTerrain(ref _headPos, s.flailRadius);
                    if (n != Vector2.zero)
                    {
                        float vn = Vector2.Dot(_headVel, n);
                        if (vn < 0f) _headVel -= n * (vn * (1f + s.terrainBounce));
                    }
                }
            }

            DealDamage();
        }

        void DealDamage()
        {
            FlailSettings s = settings;
            CombatQueries.EnemiesInCircle(_headPos, s.flailRadius, _touching);
            foreach (DummyEnemy e in _touching)
            {
                if (!_tracker.CanHit(e)) continue;
                Vector2 into = e.Position - _headPos;
                into = into.sqrMagnitude > 1e-8f ? into.normalized : _headVel.normalized;
                float speed = Mathf.Max(0f, Vector2.Dot(_headVel - e.Velocity, into));
                if (speed < s.minimumDamageSpeed) continue;

                float speedBonus = (speed - s.minimumDamageSpeed) * s.velocityDamageScale;
                float damage = Mathf.Min(s.baseDamage + speedBonus, s.maximumDamage);
                var ev = new DamageEvent
                {
                    kind = DamageKind.Weapon,
                    sourceName = DisplayName,
                    source = this,
                    baseDamage = s.baseDamage,
                    distanceMultiplier = 1f,
                    pierceMultiplier = 1f,
                    otherMultiplier = s.baseDamage > 0f ? damage / s.baseDamage : 1f,
                    finalDamage = damage,
                    sourcePosition = C.Origin,
                    hitPoint = _headPos + into * s.flailRadius,
                    hitDirection = into,
                    distance = (_headPos - C.Origin).magnitude,
                    impactSpeed = speed,
                    speedDamage = speedBonus,
                };
                Vector2 attackDir = _headVel.sqrMagnitude > 1e-6f ? _headVel.normalized : into;
                ev.knockback = s.knockback.Compute(C.Origin, attackDir, ev.hitPoint, e.Position, 1f)
                               + attackDir * (speed * s.knockbackSpeedScale * s.flailMass);
                if (!CombatDamage.Apply(e, ref ev)) continue;
                _tracker.Record(e, s.perTargetHitCooldown);
                // Bounce the head back off the enemy.
                float vn = Vector2.Dot(_headVel, into);
                if (vn > 0f) _headVel -= into * (vn * (1f + s.hitBounce) / s.flailMass);
            }
        }

        void LateUpdate()
        {
            if (!_deployed || C == null || C.Current != this) return;
            FlailSettings s = settings;
            Vector2 anchor = C.Origin;
            float dist = (_headPos - anchor).magnitude;

            // Rope sags when slack.
            float slack = Mathf.Max(0f, s.ropeLength - dist);
            Vector2 mid = (anchor + _headPos) * 0.5f + Vector2.down * (slack * 0.6f);
            _ropePts.Clear();
            _ropeCols.Clear();
            const int segs = 12;
            for (int i = 0; i <= segs; i++)
            {
                float t = (float)i / segs;
                Vector2 a = Vector2.Lerp(anchor, mid, t), b = Vector2.Lerp(mid, _headPos, t);
                _ropePts.Add(Vector2.Lerp(a, b, t));
                _ropeCols.Add(s.ropeColor);
            }
            CombatShapes.Polyline(_ropePts, 0.06f, _ropeCols);

            float relSpeed = (_headVel - C.Motor.Velocity).magnitude;
            float heat = Mathf.Clamp01((_headVel.magnitude - s.minimumDamageSpeed * 0.5f) / Mathf.Max(0.1f, s.minimumDamageSpeed));
            Color head = Color.Lerp(s.headColor, s.hotColor, _headVel.magnitude >= s.minimumDamageSpeed ? 1f : heat * 0.5f);
            CombatShapes.Disc(_headPos, s.flailRadius, head, 24);
            CombatShapes.Ring(_headPos, s.flailRadius, 0.05f, new Color(0.15f, 0.15f, 0.2f, 1f), 24);
            // Spikes.
            for (int k = 0; k < 6; k++)
            {
                Vector2 d = Surface2D.Rotate(Vector2.up, k * 60f + relSpeed * 10f);
                CombatShapes.Triangle(_headPos + Surface2D.Rotate(d, 18f) * s.flailRadius, _headPos + Surface2D.Rotate(d, -18f) * s.flailRadius,
                    _headPos + d * (s.flailRadius + 0.15f), head, head, head);
            }
        }

        public override void DrawDebug()
        {
            if (!_deployed) return;
            Vector2 o = C.Origin;
            if (CombatDebug.WeaponRange) DebugLines.Circle(o, settings.ropeLength + settings.flailRadius, CombatDebug.RangeColor, 48);
            if (CombatDebug.AttackGeometry)
            {
                DebugLines.Circle(_headPos, settings.flailRadius, CombatDebug.GeometryColor, 20);
                DebugLines.Arrow(_headPos, _headPos + _headVel * 0.15f, CombatDebug.GeometryColor, 0.15f);
            }
        }

        public override void DrawTuning(TuningGui g)
        {
            FlailSettings s = settings;
            g.Info($"Head speed now: <b>{_headVel.magnitude:0.0}</b> (damaging above {s.minimumDamageSpeed:0.0})");
            g.Header("Rope / Head");
            s.ropeLength = g.Slider("Rope Length", s.ropeLength, 0.3f, 8f);
            s.rigidRod = g.Toggle("Rigid rod (no slack)", s.rigidRod);
            s.flailRadius = g.Slider("Flail Radius", s.flailRadius, 0.05f, 1.5f);
            s.flailMass = g.Slider("Flail Mass / Inertia", s.flailMass, 0.1f, 5f);
            s.gravity = g.Slider("Gravity", s.gravity, 0f, 40f, "0.0");
            s.constraintStrength = g.Slider("Constraint Strength", s.constraintStrength, 0.05f, 1f);
            s.damping = g.Slider("Damping", s.damping, 0f, 5f);
            s.collideWithTerrain = g.Toggle("Collide with terrain", s.collideWithTerrain);
            if (s.collideWithTerrain) s.terrainBounce = g.Slider("  Terrain Bounce", s.terrainBounce, 0f, 1f);
            g.Header("Arcade Assistance");
            s.arcadeAssistance = g.Slider("Arcade Assistance (0 = physical)", s.arcadeAssistance, 0f, 1f);
            s.assistOrbitSpeed = g.Slider("Assist Orbit Speed", s.assistOrbitSpeed, 0f, 20f, "0.0");
            s.assistAcceleration = g.Slider("Assist Acceleration", s.assistAcceleration, 0f, 100f, "0");
            g.Header("Control");
            s.whipSpeed = g.Slider("Click Speed (toward cursor)", s.whipSpeed, 0f, 40f, "0.0");
            s.whipReplacesVelocity = g.Toggle("Click replaces velocity (clean aim)", s.whipReplacesVelocity);
            s.whipCooldown = g.Slider("Click Cooldown", s.whipCooldown, 0f, 1f);
            s.holdSteerAcceleration = g.Slider("Steer Acceleration (hold)", s.holdSteerAcceleration, 0f, 100f, "0");
            g.Header("Damage");
            s.baseDamage = g.Slider("Base Damage", s.baseDamage, 0f, 100f, "0.0");
            s.minimumDamageSpeed = g.Slider("Minimum Damage Speed", s.minimumDamageSpeed, 0f, 20f, "0.0");
            s.velocityDamageScale = g.Slider("Velocity Damage Scale", s.velocityDamageScale, 0f, 5f);
            s.maximumDamage = g.Slider("Maximum Damage", s.maximumDamage, 0f, 150f, "0");
            g.Info($"  At click speed: {Mathf.Min(s.baseDamage + Mathf.Max(0f, s.whipSpeed - s.minimumDamageSpeed) * s.velocityDamageScale, s.maximumDamage):0.0}");
            s.perTargetHitCooldown = g.Slider("Per Target Hit Cooldown", s.perTargetHitCooldown, 0f, 2f);
            s.hitBounce = g.Slider("Bounce Off Enemies", s.hitBounce, 0f, 1f);
            g.Header("Knockback");
            g.Knockback(s.knockback, 20f);
            s.knockbackSpeedScale = g.Slider("Knockback Per Speed", s.knockbackSpeedScale, 0f, 2f);
        }
    }
}
