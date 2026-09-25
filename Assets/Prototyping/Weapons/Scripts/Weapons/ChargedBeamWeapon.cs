using System;
using System.Collections.Generic;
using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>Tuning shared by the charged lane beams (eye beam, holy beams).</summary>
    [Serializable]
    public class ChargedBeamSettings
    {
        [Min(0f)] public float damage = 55f;
        public RepeatHitMode repeatMode = RepeatHitMode.HitOnce;
        [Min(0.03f)] public float repeatInterval = 0.12f;

        [Tooltip("Charge time before the beam fires. It fires automatically once charged.")]
        [Min(0f)] public float chargeDuration = 0.3f;
        [Min(0.02f)] public float activeDuration = 0.3f;
        [Min(0f)] public float recoveryDuration = 0.15f;
        [Tooltip("Extra wait after recovery (the charge is the main gate).")]
        [Min(0f)] public float cooldown;
        [Tooltip("Holding fire starts charging the next beam as soon as it can.")]
        public bool refireWhileHeld = true;

        [Min(0.1f)] public float beamWidth = 1.2f;
        public RangeSettings extent = new RangeSettings(16f, 0.75f);
        [Tooltip("Surfaces stop the part of the beam they're in front of; the rest continues.")]
        public bool blockedByTerrain = true;
        [Tooltip("Width of each independently blocked strip (smaller = finer splitting, more raycasts).")]
        [Range(0.04f, 0.5f)] public float laneSpacing = 0.1f;

        public bool lockDuringCharge;
        public bool lockDuringActive;
        public bool lockDuringRecovery;
        [Tooltip("Firing while airborne grants one air jump (even with air jumps off), for jump-beam-jump.")]
        public bool airJumpAfterBeam;

        public DistanceFalloff distanceFalloff = new DistanceFalloff(FalloffMode.None, 0.5f, 0.6f);
        public PierceSettings pierce = new PierceSettings(true, 0, PierceFalloffMode.None, 0.85f);
        public KnockbackSettings knockback = new KnockbackSettings(5f, KnockbackDirection.AlongAttackDirection);
        public Color color = new Color(0.45f, 0.95f, 1f, 0.85f);
    }

    /// <summary>
    /// Charge -> fire -> recover beam made of <see cref="LaneBeam"/> arms. Subclasses decide where the arms are
    /// (one toward the cursor, or world-aligned through the player). The player's movement can be locked in each
    /// phase through the motor's external lock; the tuning is never touched.
    /// </summary>
    public abstract class ChargedBeamWeapon : PrototypeWeapon
    {
        protected abstract ChargedBeamSettings Beam { get; }

        /// <summary>Fill origin + direction for every arm this frame.</summary>
        protected abstract void GetArms(List<(Vector2 origin, Vector2 dir)> arms);

        /// <summary>Called when charging starts, then every frame while not idle (capture / track aim).</summary>
        protected virtual void OnChargeStart() { }
        protected virtual void TrackAim() { }

        protected enum State { Idle, Charging, Active, Recovery }

        protected State CurrentState { get; private set; }
        float _stateTime;
        bool _firedInAir;
        HitTracker _tracker = new HitTracker();
        readonly List<(Vector2 origin, Vector2 dir)> _arms = new List<(Vector2, Vector2)>();
        readonly List<LaneBeam> _beams = new List<LaneBeam>();
        readonly List<CombatQueries.LineHit> _hits = new List<CombatQueries.LineHit>();

        public override WeaponPhase Phase
        {
            get
            {
                switch (CurrentState)
                {
                    case State.Charging: return WeaponPhase.Windup;
                    case State.Active: return WeaponPhase.Active;
                    case State.Recovery: return WeaponPhase.Recovery;
                    default: return CooldownReady ? WeaponPhase.Ready : WeaponPhase.CoolingDown;
                }
            }
        }

        public override float PhaseRemaining
        {
            get
            {
                ChargedBeamSettings s = Beam;
                switch (CurrentState)
                {
                    case State.Charging: return s.chargeDuration - _stateTime;
                    case State.Active: return s.activeDuration - _stateTime;
                    case State.Recovery: return s.recoveryDuration - _stateTime;
                    default: return base.PhaseRemaining;
                }
            }
        }

        public override float PhaseProgress
        {
            get
            {
                ChargedBeamSettings s = Beam;
                switch (CurrentState)
                {
                    case State.Charging: return Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, s.chargeDuration));
                    case State.Active: return 1f - Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, s.activeDuration));
                    case State.Recovery: return Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, s.recoveryDuration));
                    default: return base.PhaseProgress;
                }
            }
        }

        protected bool MovementLocked => C != null && C.Motor.IsMovementLocked;

        public override void BeginFire() => TryStart();

        public override void HoldFire(float dt)
        {
            if (Beam.refireWhileHeld) TryStart();
        }

        void TryStart()
        {
            if (CurrentState != State.Idle || !CooldownReady) return;
            CountActivation();
            _tracker = new HitTracker();
            _firedInAir = false;
            OnChargeStart();
            Enter(State.Charging);
        }

        public override void Cancel()
        {
            if (CurrentState != State.Idle) StartCooldown(Beam.cooldown);
            CurrentState = State.Idle;
            SetLock(false);
        }

        void OnDisable()
        {
            if (C != null) SetLock(false);
        }

        void Enter(State s)
        {
            CurrentState = s;
            _stateTime = 0f;
            ChargedBeamSettings b = Beam;
            SetLock(s == State.Charging ? b.lockDuringCharge : s == State.Active ? b.lockDuringActive : s == State.Recovery && b.lockDuringRecovery);
        }

        void SetLock(bool locked)
        {
            if (locked) C.Motor.AddMovementLock(this);
            else C.Motor.RemoveMovementLock(this);
        }

        public override void Tick(float dt, bool selected)
        {
            if (CurrentState == State.Idle) return;
            ChargedBeamSettings s = Beam;
            _stateTime += dt;
            TrackAim();
            ComputeBeams();

            switch (CurrentState)
            {
                case State.Charging:
                    if (_stateTime >= s.chargeDuration) Enter(State.Active);
                    break;
                case State.Active:
                    if (!C.Motor.IsAttached) _firedInAir = true;
                    DealDamage();
                    if (_stateTime >= s.activeDuration) Enter(State.Recovery);
                    break;
                case State.Recovery:
                    if (_stateTime >= s.recoveryDuration) Finish();
                    break;
            }
        }

        void Finish()
        {
            CurrentState = State.Idle;
            SetLock(false);
            StartCooldown(Beam.cooldown);
            if (Beam.airJumpAfterBeam && _firedInAir && !C.Motor.IsAttached) C.Motor.GrantBonusAirJump();
        }

        void ComputeBeams()
        {
            ChargedBeamSettings s = Beam;
            _arms.Clear();
            GetArms(_arms);
            while (_beams.Count < _arms.Count) _beams.Add(new LaneBeam());
            for (int i = 0; i < _arms.Count; i++)
                _beams[i].Compute(_arms[i].origin, _arms[i].dir, s.beamWidth, s.extent.maxRange, s.blockedByTerrain, s.laneSpacing);
        }

        void DealDamage()
        {
            ChargedBeamSettings s = Beam;
            float interval = s.repeatMode == RepeatHitMode.HitOnce ? -1f : s.repeatInterval;
            for (int a = 0; a < _arms.Count; a++)
            {
                LaneBeam beam = _beams[a];
                beam.Hits(_hits);
                int max = s.pierce.MaxTargets;
                for (int i = 0; i < _hits.Count && i < max; i++)
                {
                    CombatQueries.LineHit h = _hits[i];
                    if (!_tracker.CanHit(h.enemy)) continue; // overlapping arms only hit once
                    var e = DamageEvent.Weapon(DisplayName, this, s.damage, h.along, h.along / s.extent.maxRange,
                        s.distanceFalloff, i, s.pierce);
                    e.sourcePosition = beam.Origin;
                    e.hitPoint = h.point;
                    e.hitDirection = beam.Direction;
                    e.knockback = s.knockback.Compute(beam.Origin, beam.Direction, h.point, h.enemy.Position, e.FalloffMultiplier);
                    CombatDamage.Apply(h.enemy, ref e);
                    _tracker.Record(h.enemy, interval);
                }
            }
        }

        protected virtual void LateUpdate()
        {
            if (C == null || CurrentState == State.Idle) return;
            ChargedBeamSettings s = Beam;
            for (int a = 0; a < _arms.Count && a < _beams.Count; a++)
            {
                LaneBeam beam = _beams[a];
                switch (CurrentState)
                {
                    case State.Charging:
                    {
                        float k = Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, s.chargeDuration));
                        beam.DrawGuides(CombatShapes.WithAlpha(s.color, 0.2f + 0.5f * k), s.extent, 0.04f + 0.06f * k);
                        break;
                    }
                    case State.Active:
                        beam.Draw(CombatShapes.WithAlpha(s.color, s.color.a * (0.9f + 0.1f * Mathf.Sin(Time.time * 50f))), s.extent, 1f, 0.35f);
                        break;
                    case State.Recovery:
                    {
                        float k = 1f - Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, s.recoveryDuration));
                        beam.Draw(CombatShapes.WithAlpha(s.color, s.color.a * k), s.extent, k);
                        break;
                    }
                }
            }
            DrawCharge();
            if (MovementLocked) CombatShapes.Ring(C.Origin, C.Motor.Radius + 0.2f, 0.06f, CombatShapes.WithAlpha(s.color, 0.8f));
        }

        /// <summary>Charge glow around the eye.</summary>
        protected virtual void DrawCharge()
        {
            if (CurrentState != State.Charging) return;
            ChargedBeamSettings s = Beam;
            float k = Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, s.chargeDuration));
            CombatShapes.Ring(C.Origin, C.Motor.Radius + 0.5f * (1f - k) + 0.1f, 0.05f + 0.08f * k, CombatShapes.WithAlpha(s.color, 0.3f + 0.6f * k));
        }

        public override void DrawDebug()
        {
            if (!CombatDebug.AttackGeometry && !CombatDebug.WeaponRange) return;
            if (CurrentState == State.Idle)
            {
                // Preview where the beam would go right now.
                OnChargeStart();
                ComputeBeams();
            }
            Color c = CombatDebug.AttackGeometry ? CombatDebug.GeometryColor : CombatDebug.RangeColor;
            for (int i = 0; i < _arms.Count && i < _beams.Count; i++)
            {
                if (CombatDebug.AttackGeometry) _beams[i].DrawDebug(c);
                else _beams[i].DrawGuides(CombatDebug.RangeColor, Beam.extent, 0.03f);
            }
        }

        /// <summary>Shared tuning UI; subclasses add their own section first.</summary>
        protected void DrawBeamTuning(TuningGui g)
        {
            ChargedBeamSettings s = Beam;
            g.Header("Damage");
            s.damage = g.Slider("Damage", s.damage, 0f, 300f, "0");
            s.repeatMode = g.Enum("Repeat", s.repeatMode);
            if (s.repeatMode == RepeatHitMode.Repeated) s.repeatInterval = g.Slider("  Repeat Interval", s.repeatInterval, 0.03f, 0.5f);
            g.Header("Timing");
            s.chargeDuration = g.Slider("Charge Duration", s.chargeDuration, 0f, 2f);
            s.activeDuration = g.Slider("Active Duration", s.activeDuration, 0.02f, 2f);
            s.recoveryDuration = g.Slider("Recovery Duration", s.recoveryDuration, 0f, 1f);
            s.cooldown = g.Slider("Extra Cooldown", s.cooldown, 0f, 4f);
            s.refireWhileHeld = g.Toggle("Re-charge while held", s.refireWhileHeld);
            g.Header("Movement");
            s.lockDuringCharge = g.Toggle("Lock movement while charging", s.lockDuringCharge);
            s.lockDuringActive = g.Toggle("Lock movement while firing", s.lockDuringActive);
            s.lockDuringRecovery = g.Toggle("Lock movement during recovery", s.lockDuringRecovery);
            s.airJumpAfterBeam = g.Toggle("Air jump after firing in the air", s.airJumpAfterBeam);
            g.Header("Beam");
            s.beamWidth = g.Slider("Beam Width", s.beamWidth, 0.1f, 6f);
            s.blockedByTerrain = g.Toggle("Blocked / split by terrain", s.blockedByTerrain);
            if (s.blockedByTerrain) s.laneSpacing = g.Slider("  Split Resolution (strip width)", s.laneSpacing, 0.04f, 0.5f);
            g.Header("Range / Falloff");
            g.Range(s.extent, 60f, "Maximum Extent");
            g.Falloff(s.distanceFalloff);
            g.Header("Piercing");
            g.Pierce(s.pierce);
            g.Header("Knockback");
            g.Knockback(s.knockback, 30f);
        }
    }
}
