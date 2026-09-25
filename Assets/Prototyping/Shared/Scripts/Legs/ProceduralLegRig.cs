using UnityEngine;

namespace Televised.Prototyping.Shared
{
    /// <summary>
    /// Cosmetic procedural legs. Reads the motor's surface state (same Surface2D API as gameplay)
    /// and animates virtual feet: planting, alternating steps, attach reach, detach trail and an
    /// airborne tucked pose. Nothing here feeds back into movement.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class ProceduralLegRig : MonoBehaviour
    {
        [SerializeField] PlayerMotor2D motor;

        [Header("Rendering")]
        [SerializeField] Material legMaterial;
        [SerializeField] Sprite footSprite;
        [Tooltip("2 or 4 legs. Can be changed during Play Mode.")]
        [SerializeField, Range(2, 4)] int legCount = 2;
        [SerializeField] Color legColor = new Color(0.85f, 0.35f, 0.4f);
        [SerializeField] Color footColor = new Color(0.95f, 0.5f, 0.55f);
        [SerializeField] float lineWidth = 0.07f;
        [SerializeField] float footSize = 0.14f;
        [SerializeField] int sortingOrder = 5;

        [Header("Shape")]
        [SerializeField] float legLength = 0.95f;
        [Tooltip("Socket offset along the body tangent.")]
        [SerializeField] float socketSpread = 0.28f;
        [Tooltip("Socket offset toward the surface.")]
        [SerializeField] float socketDepth = 0.22f;

        [Header("Gait")]
        [Tooltip("Distance along the surface between the outer feet (roughly the player diameter).")]
        [SerializeField] float footSpacing = 1.0f;
        [SerializeField] float stepThreshold = 0.35f;
        [SerializeField] float stepDuration = 0.14f;
        [SerializeField] float stepHeight = 0.2f;
        [Tooltip("Seconds of crawl velocity the desired foot position leads by.")]
        [SerializeField] float stepLead = 0.1f;
        [Tooltip("Step duration multiplier at full crawl speed.")]
        [SerializeField, Range(0.2f, 1f)] float fastStepScale = 0.6f;
        [Tooltip("Feet farther than stepThreshold * this step immediately, ignoring alternation (recovery).")]
        [SerializeField] float forceStepFactor = 2.2f;
        [SerializeField] float idleSettleDelay = 0.25f;
        [SerializeField] float idleSettleThreshold = 0.06f;

        [Header("Grapple")]
        [Tooltip("While the grapple leg is shooting or latched, the other legs are hidden (and hold a quiet pose " +
                 "underneath, so they reappear cleanly when the grapple retracts).")]
        [SerializeField] bool hideOtherLegsWhileGrappling = true;

        [Header("Attach / Airborne")]
        [SerializeField] float attachReachDuration = 0.07f;
        [SerializeField] float attachStagger = 0.03f;
        [Tooltip("How far (fraction of leg length) the tucked feet sit from the body while flying.")]
        [SerializeField] float airRetract = 0.35f;
        [SerializeField] float airFollowSharpness = 14f;
        [SerializeField] float airWobble = 0.06f;
        [Tooltip("Airborne feet start reaching for an approaching surface within this gap. 0 = off.")]
        [SerializeField] float anticipateDistance = 1.2f;

        class Leg
        {
            public LineRenderer line;
            public SpriteRenderer foot;
            public float side;          // -1 behind, +1 ahead (along +path)
            public float offsetFactor;  // fraction of footSpacing from the center
            public int group;           // legs in the same group may step together
            public Vector2 footPos;
            public Vector2 stepFrom;
            public bool stepping;
            public float stepT, stepDur, stepH;
            public bool needsStep;
            public bool isGrapple;      // currently driven by the grapple (drawn out to the grapple tip)
            public float needDistance;
        }

        Leg[] _legs;
        int _builtCount;
        int _lastGroupStepped = -1;
        float _idleTimer;
        Vector2 _up = Vector2.up;
        Vector2 _tan = Vector2.left;
        Transform _container;
        Surface2D _carrySurface;     // surface the feet were planted on last frame
        Matrix4x4 _carryMatrix;      // its transform last frame

        public bool HideOtherLegsWhileGrappling
        {
            get => hideOtherLegsWhileGrappling;
            set => hideOtherLegsWhileGrappling = value;
        }

        public int LegCount { get => legCount >= 3 ? 4 : 2; set => legCount = value >= 3 ? 4 : 2; }

        void Awake()
        {
            if (motor == null) motor = GetComponentInParent<PlayerMotor2D>();
        }

        void OnEnable()
        {
            if (motor != null) { motor.Attached += OnAttached; motor.Detached += OnDetached; motor.AirJumped += OnAirJumped; }
            if (_container != null) _container.gameObject.SetActive(true);
        }

        void OnDisable()
        {
            if (motor != null) { motor.Attached -= OnAttached; motor.Detached -= OnDetached; motor.AirJumped -= OnAirJumped; }
            if (_container != null) _container.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- build

        void Build()
        {
            if (_container != null) Destroy(_container.gameObject);
            _container = new GameObject("Legs (runtime)").transform;
            _container.SetParent(transform, false);

            int count = legCount >= 3 ? 4 : 2;
            _legs = new Leg[count];
            for (int i = 0; i < count; i++)
            {
                var leg = new Leg();
                if (count == 2)
                {
                    leg.side = i == 0 ? -1f : 1f;
                    leg.offsetFactor = 0.5f;
                    leg.group = i;
                }
                else
                {
                    // back-outer, back-inner, front-inner, front-outer; diagonal pairs alternate.
                    leg.side = i < 2 ? -1f : 1f;
                    leg.offsetFactor = (i == 0 || i == 3) ? 0.75f : 0.25f;
                    leg.group = (i == 0 || i == 2) ? 0 : 1;
                }

                var go = new GameObject($"Leg{i}");
                go.transform.SetParent(_container, false);
                leg.line = go.AddComponent<LineRenderer>();
                leg.line.useWorldSpace = true;
                leg.line.positionCount = 3;
                leg.line.widthMultiplier = lineWidth;
                leg.line.numCapVertices = 4;
                leg.line.numCornerVertices = 4;
                leg.line.sharedMaterial = legMaterial;
                leg.line.startColor = leg.line.endColor = legColor;
                leg.line.sortingOrder = sortingOrder;

                var footGo = new GameObject("Foot");
                footGo.transform.SetParent(go.transform, false);
                footGo.transform.localScale = Vector3.one * footSize;
                leg.foot = footGo.AddComponent<SpriteRenderer>();
                leg.foot.sprite = footSprite;
                leg.foot.color = footColor;
                leg.foot.sortingOrder = sortingOrder + 1;

                leg.footPos = motor != null ? motor.Position : (Vector2)transform.position;
                _legs[i] = leg;
            }
            _builtCount = legCount;
        }

        // ---------------------------------------------------------------- events

        void OnAttached(Surface2D surface, bool transfer)
        {
            if (_legs == null || transfer) return;
            // Legs extend rapidly toward their surface targets, slightly staggered.
            for (int i = 0; i < _legs.Length; i++)
            {
                Leg leg = _legs[i];
                if (leg.isGrapple) continue;
                leg.stepping = true;
                leg.stepFrom = leg.footPos;
                leg.stepT = 0f;
                leg.stepDur = attachReachDuration + i * attachStagger;
                leg.stepH = stepHeight * 0.3f;
            }
            _lastGroupStepped = -1;
        }

        void OnDetached(Surface2D surface)
        {
            if (_legs == null) return;
            foreach (Leg leg in _legs) leg.stepping = false; // feet release
        }

        void OnAirJumped(Vector2 dir)
        {
            if (_legs == null) return;
            // Kick: feet snap out opposite the launch direction, then trail back in via the airborne follow.
            Vector2 center = motor.Position;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            foreach (Leg leg in _legs)
            {
                if (leg.isGrapple) continue;
                leg.stepping = false;
                leg.footPos = center - dir * (legLength * 0.95f) + perp * (leg.side * (0.2f + 0.25f * leg.offsetFactor));
            }
        }

        // ---------------------------------------------------------------- update

        void LateUpdate()
        {
            if (motor == null) return;
            if (_legs == null || _builtCount != legCount) Build();

            float dt = Time.deltaTime;
            Vector2 center = motor.Position;
            bool attached = motor.IsAttached && motor.CurrentSurface != null;

            if (attached)
            {
                _up = motor.SmoothedNormal;
            }
            else
            {
                // Drift the body frame back toward screen-up while flying.
                float k = 1f - Mathf.Exp(-3f * dt);
                _up = Surface2D.Rotate(_up, Vector2.SignedAngle(_up, Vector2.up) * k).normalized;
            }
            _tan = new Vector2(-_up.y, _up.x);

            // The front-most leg doubles as the grapple leg while the grapple is out.
            GrappleController grapple = motor.Grapple;
            bool grappling = grapple != null && grapple.IsActive;
            for (int i = 0; i < _legs.Length; i++) _legs[i].isGrapple = grappling && i == _legs.Length - 1;

            CarryFeetWithSurface(attached ? motor.CurrentSurface : null);

            // Other legs are hidden only while the grapple is actually out/holding (not while it retracts).
            bool hideOthers = grappling && hideOtherLegsWhileGrappling &&
                              (grapple.State == GrappleState.Shooting || grapple.State == GrappleState.Latched);

            if (hideOthers) HoldOtherLegs(center, attached);
            else if (attached) UpdateAttached(dt);
            else UpdateAirborne(center, dt);

            if (grappling)
            {
                Leg g = _legs[_legs.Length - 1];
                g.stepping = false;
                g.footPos = grapple.TipPosition;
            }

            for (int i = 0; i < _legs.Length; i++)
            {
                Leg leg = _legs[i];
                bool visible = !(hideOthers && !leg.isGrapple);
                leg.line.enabled = visible;
                leg.foot.enabled = visible;
                Draw(leg, Socket(leg, center));
            }
        }

        /// <summary>
        /// Planted feet (and steps in progress) move with a moving / rotating surface, so they stay put
        /// on it instead of lagging behind and re-stepping every frame.
        /// </summary>
        void CarryFeetWithSurface(Surface2D surface)
        {
            if (surface != null && surface == _carrySurface)
            {
                Matrix4x4 now = surface.transform.localToWorldMatrix;
                if (now != _carryMatrix)
                {
                    Matrix4x4 delta = now * _carryMatrix.inverse;
                    foreach (Leg leg in _legs)
                    {
                        if (leg.isGrapple) continue;
                        leg.footPos = delta.MultiplyPoint3x4(leg.footPos);
                        leg.stepFrom = delta.MultiplyPoint3x4(leg.stepFrom);
                    }
                }
            }
            _carrySurface = surface;
            _carryMatrix = surface != null ? surface.transform.localToWorldMatrix : Matrix4x4.identity;
        }

        /// <summary>
        /// While the other legs are hidden during a grapple, keep them in a sensible pose so they reappear cleanly:
        /// planted feet stay where they are on a surface; in the air they sit tucked under the body.
        /// </summary>
        void HoldOtherLegs(Vector2 center, bool attached)
        {
            foreach (Leg leg in _legs)
            {
                if (leg.isGrapple) continue;
                leg.stepping = false;
                if (attached) continue;
                leg.footPos = center
                              - _up * (motor.Radius * 0.55f + legLength * airRetract)
                              + _tan * (leg.side * (0.12f + 0.18f * leg.offsetFactor));
            }
        }

        Vector2 Socket(Leg leg, Vector2 center) =>
            center + _tan * (leg.side * socketSpread * (0.5f + leg.offsetFactor)) - _up * socketDepth;

        SurfaceSample DesiredFoot(Leg leg)
        {
            float offset = leg.side * footSpacing * leg.offsetFactor + motor.SurfaceSpeed * stepLead;
            return motor.CurrentSurface.SampleAt(motor.PathPosition + offset);
        }

        void UpdateAttached(float dt)
        {
            float speedAbs = Mathf.Abs(motor.SurfaceSpeed);
            float refSpeed = motor.Tuning != null ? Mathf.Max(0.01f, motor.Tuning.surfaceMoveSpeed) : 5f;
            float durScale = Mathf.Lerp(1f, fastStepScale, Mathf.Clamp01(speedAbs / refSpeed));
            _idleTimer = speedAbs < 0.05f ? _idleTimer + dt : 0f;

            // 1. Advance steps in progress.
            foreach (Leg leg in _legs)
            {
                if (!leg.stepping || leg.isGrapple) continue;
                SurfaceSample target = DesiredFoot(leg);
                leg.stepT += dt / Mathf.Max(0.01f, leg.stepDur);
                float t = Mathf.Clamp01(leg.stepT);
                float e = t * t * (3f - 2f * t);
                leg.footPos = Vector2.Lerp(leg.stepFrom, target.point, e) + target.normal * (Mathf.Sin(t * Mathf.PI) * leg.stepH);
                if (leg.stepT >= 1f)
                {
                    leg.footPos = target.point;
                    leg.stepping = false;
                    _lastGroupStepped = leg.group;
                }
            }

            // 2. Which planted feet want to move?
            foreach (Leg leg in _legs)
            {
                leg.needsStep = false;
                if (leg.stepping || leg.isGrapple) continue;
                leg.needDistance = Vector2.Distance(leg.footPos, DesiredFoot(leg).point);
                leg.needsStep = leg.needDistance > stepThreshold ||
                                (_idleTimer > idleSettleDelay && leg.needDistance > idleSettleThreshold);
            }

            // 3. Start steps, alternating groups.
            foreach (Leg leg in _legs)
            {
                if (leg.stepping || !leg.needsStep || leg.isGrapple) continue;

                bool force = leg.needDistance > stepThreshold * forceStepFactor;
                bool otherStepping = false, otherNeeds = false;
                foreach (Leg o in _legs)
                {
                    if (o.group == leg.group || o.isGrapple) continue;
                    otherStepping |= o.stepping;
                    otherNeeds |= o.needsStep;
                }

                bool myTurn = _lastGroupStepped != leg.group || !otherNeeds;
                if (force || (!otherStepping && myTurn))
                {
                    leg.stepping = true;
                    leg.stepFrom = leg.footPos;
                    leg.stepT = 0f;
                    leg.stepDur = stepDuration * durScale;
                    leg.stepH = _idleTimer > idleSettleDelay ? stepHeight * 0.4f : stepHeight;
                }
            }
        }

        void UpdateAirborne(Vector2 center, float dt)
        {
            Vector2 v = motor.Velocity;
            Vector2 back = v.sqrMagnitude > 0.04f ? -v.normalized : -_up;
            Vector2 side = new Vector2(-back.y, back.x);
            float follow = 1f - Mathf.Exp(-airFollowSharpness * dt);

            // Anticipation: nearest approaching, non-blocked surface.
            SurfaceCandidate? reach = null;
            if (anticipateDistance > 0f && motor.Sensor != null && motor.Tuning != null)
            {
                float best = anticipateDistance;
                foreach (SurfaceCandidate c in motor.Sensor.Candidates)
                {
                    if (c.gap >= best || c.approachSpeed <= 0.5f) continue;
                    if (!c.Surface.Attachable) continue; // never reach for hazards / non-attachable surfaces
                    if (!motor.Tuning.AllowsSurfaceNormal(c.sample.normal)) continue; // too steep to stick to
                    if (motor.Attachment != null && motor.Attachment.IsBlocked(c.Surface, motor.Tuning)) continue;
                    best = c.gap;
                    reach = c;
                }
            }

            for (int i = 0; i < _legs.Length; i++)
            {
                Leg leg = _legs[i];
                if (leg.isGrapple) continue;
                float wobble = Mathf.Sin(Time.time * 13f + i * 1.7f) * airWobble;
                Vector2 target = center
                                 + back * (motor.Radius * 0.55f + legLength * airRetract)
                                 + side * (leg.side * (0.12f + 0.18f * leg.offsetFactor) + wobble);

                if (reach is SurfaceCandidate c)
                {
                    SurfaceSample s = c.Surface.SampleAt(c.sample.pathPosition + leg.side * footSpacing * leg.offsetFactor);
                    float w = 0.85f * (1f - Mathf.Clamp01(c.gap / anticipateDistance));
                    target = Vector2.Lerp(target, s.point, w);
                }

                leg.footPos = Vector2.Lerp(leg.footPos, target, follow);
            }
        }

        void Draw(Leg leg, Vector2 socket)
        {
            Vector2 foot = leg.footPos;
            Vector2 d = foot - socket;
            float dist = d.magnitude;
            float half = legLength * 0.5f;

            Vector2 knee = socket + d * 0.5f;
            if (dist > 1e-4f && dist < legLength)
            {
                float h = Mathf.Sqrt(Mathf.Max(0f, half * half - dist * dist * 0.25f));
                Vector2 perp = new Vector2(-d.y, d.x) / dist;
                Vector2 hint = _up + _tan * (leg.side * 0.5f);
                if (Vector2.Dot(perp, hint) < 0f) perp = -perp;
                knee += perp * h;
            }

            leg.line.SetPosition(0, socket);
            leg.line.SetPosition(1, knee);
            leg.line.SetPosition(2, foot);
            leg.foot.transform.position = new Vector3(foot.x, foot.y, 0f);
        }
    }
}
