using System;
using UnityEngine;

namespace Televised.Prototyping.Shared
{
    /// <summary>
    /// Owns the player's true position and the Attached / Airborne state machine.
    ///
    /// Attached: the root is explicitly constrained to surfacePoint + normal * radius at a tracked
    /// path position; A/D moves that path position. No friction or gravity is involved.
    /// Airborne: simple ballistic integration with sub-stepping, collision push-out against
    /// Surface2D geometry, and attachment decided by SurfaceAttachmentController.
    ///
    /// Leg animation, eye aiming and debug drawing only READ from this component.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PlayerMotor2D : MonoBehaviour
    {
        [SerializeField] MovementTuning tuning;
        [Tooltip("Off: Play Mode edits go to a runtime copy of the tuning asset and are discarded on exit.\n" +
                 "On: edits go straight into the asset and persist.")]
        [SerializeField] bool persistTuningChanges = false;
        [SerializeField] SurfaceSensor sensor;
        [SerializeField] SurfaceAttachmentController attachment;
        [SerializeField] JumpDirectionResolver jumpResolver;
        [SerializeField] SurfaceInputResolver inputResolver;
        [SerializeField] GrappleController grapple;
        [SerializeField] Camera aimCamera;
        [Tooltip("Falling below this Y respawns the player at its start position.")]
        [SerializeField] float killY = -60f;

        // ---------------------------------------------------------------- state (read-only API)
        /// <summary>The tuning currently in use: the asset itself, or its runtime copy when not persisting.</summary>
        public MovementTuning Tuning => tuning;
        /// <summary>The tuning asset on disk.</summary>
        public MovementTuning TuningAsset => _tuningAsset;
        public bool PersistTuningChanges
        {
            get => persistTuningChanges;
            set => SetPersistTuning(value);
        }
        public SurfaceSensor Sensor => sensor;
        public SurfaceAttachmentController Attachment => attachment;
        public JumpDirectionResolver JumpResolver => jumpResolver;
        public SurfaceInputResolver InputResolver => inputResolver;
        public GrappleController Grapple => grapple;

        public PlayerMovementState State { get; private set; } = PlayerMovementState.Airborne;
        public bool IsAttached => State == PlayerMovementState.Attached;
        public float Radius => tuning.playerRadius;
        public Vector2 Position => _position;
        /// <summary>World velocity. When attached this is measured from frame displacement.</summary>
        public Vector2 Velocity => _velocity;

        public Surface2D CurrentSurface => _surface;
        public float PathPosition => _pathPos;
        public SurfaceSample CurrentSample => _sample;
        /// <summary>Per-vertex smoothed normal at the contact point.</summary>
        public Vector2 LocalNormal => _sample.normal;
        /// <summary>Virtual-foot (chord) normal with temporal smoothing. Also used as the visual "up".</summary>
        public Vector2 SmoothedNormal => _smoothedNormal;
        public Vector2 Tangent => _sample.tangent;
        public SurfaceSample VirtualFootBack => _footBack;
        public SurfaceSample VirtualFootFront => _footFront;
        /// <summary>Velocity of the attached surface itself at the contact (moving / rotating platforms).</summary>
        public Vector2 PlatformVelocity => _platformVelocity;
        /// <summary>Signed crawl speed along the path (+ = counter-clockwise).</summary>
        public float SurfaceSpeed => _surfaceSpeed;
        public int TraversalSign => inputResolver != null ? inputResolver.LastSign : 0;
        public Vector2 AttachOffset => _attachOffset;
        public float TimeSinceAttach => Time.time - _attachTime;
        public float TimeSinceDetach => Time.time - _detachTime;

        public Vector2 CursorWorld { get; private set; }
        public Vector2 AimDirection { get; private set; } = Vector2.right;
        public Vector2 LastJumpDirection { get; private set; }
        public Vector2 LastJumpOrigin { get; private set; }
        public JumpResult LastJumpResult { get; private set; }
        public Vector2 SpawnPosition { get; set; }

        /// <summary>(surface, isTransfer). isTransfer = crawled from one surface onto another.</summary>
        public event Action<Surface2D, bool> Attached;
        public event Action<Surface2D> Detached;
        /// <summary>Fired on an in-air (double) jump with the launch direction.</summary>
        public event Action<Vector2> AirJumped;
        /// <summary>Fired whenever the player is respawned (kill plane, or anyone calling Respawn()).</summary>
        public event Action Respawned;

        public int AirJumpsUsed => _airJumpsUsed;
        public int AirJumpsRemaining => tuning.enableAirJumps ? Mathf.Max(0, tuning.airJumpCount - _airJumpsUsed) : 0;
        /// <summary>Direction an air jump would launch in right now (for debug preview).</summary>
        public Vector2 AirJumpPreviewDirection =>
            jumpResolver != null ? jumpResolver.ResolveAirJump(PrototypeInput.MoveVector, AimDirection, tuning) : Vector2.up;

        Vector2 _position;
        Vector2 _velocity;
        Surface2D _surface;
        float _pathPos;
        SurfaceSample _sample;
        SurfaceSample _footBack, _footFront;
        Vector2 _smoothedNormal = Vector2.up;
        float _surfaceSpeed;
        Vector2 _attachOffset;
        float _attachTime = -999f, _detachTime = -999f;
        float _jumpBuffer;
        Vector2 _platformVelocity;
        int _airJumpsUsed;
        MovementTuning _tuningAsset;
        MovementTuning _runtimeTuning;

        // ---------------------------------------------------------------- lifecycle

        void Awake()
        {
            if (tuning == null)
            {
                Debug.LogWarning("[PlayerMotor2D] No MovementTuning assigned, using defaults.", this);
                tuning = ScriptableObject.CreateInstance<MovementTuning>();
            }
            _tuningAsset = tuning;
            if (!persistTuningChanges) UseRuntimeTuningCopy();
            if (sensor == null) sensor = GetComponentInChildren<SurfaceSensor>();
            if (sensor == null) sensor = gameObject.AddComponent<SurfaceSensor>();
            if (attachment == null) attachment = GetOrAdd<SurfaceAttachmentController>();
            if (jumpResolver == null) jumpResolver = GetOrAdd<JumpDirectionResolver>();
            if (inputResolver == null) inputResolver = GetOrAdd<SurfaceInputResolver>();
            if (grapple == null) grapple = GetOrAdd<GrappleController>();
            grapple.Latched += OnGrappleLatched;

            _position = transform.position;
            SpawnPosition = _position;
        }

        void OnDestroy()
        {
            if (_runtimeTuning != null) Destroy(_runtimeTuning);
            if (grapple != null) grapple.Latched -= OnGrappleLatched;
        }

        // ---------------------------------------------------------------- tuning persistence

        void UseRuntimeTuningCopy()
        {
            if (_runtimeTuning == null)
            {
                _runtimeTuning = Instantiate(_tuningAsset);
                _runtimeTuning.name = _tuningAsset.name + " (Runtime Copy)";
            }
            else
            {
                CopyTuning(_tuningAsset, _runtimeTuning);
            }
            tuning = _runtimeTuning;
        }

        void SetPersistTuning(bool persist)
        {
            if (persist == persistTuningChanges) return;
            persistTuningChanges = persist;
            if (persist)
            {
                // Keep what's been tuned so far, then edit the asset directly from now on.
                if (tuning != _tuningAsset) CopyTuning(tuning, _tuningAsset);
                tuning = _tuningAsset;
                MarkTuningAssetDirty();
            }
            else
            {
                UseRuntimeTuningCopy(); // starts from the asset's current values
            }
        }

        /// <summary>Write the runtime copy's values into the asset (no-op when already persisting).</summary>
        public void SaveTuningToAsset()
        {
            if (tuning == _tuningAsset) return;
            CopyTuning(tuning, _tuningAsset);
            MarkTuningAssetDirty();
        }

        /// <summary>Discard runtime edits and reload the asset's values (no-op when persisting).</summary>
        public void RevertTuningToAsset()
        {
            if (tuning == _tuningAsset) return;
            CopyTuning(_tuningAsset, tuning);
        }

        static void CopyTuning(MovementTuning from, MovementTuning to) =>
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(from), to);

        void MarkTuningAssetDirty()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(_tuningAsset);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(_tuningAsset);
#endif
        }

        T GetOrAdd<T>() where T : Component
        {
            T c = GetComponent<T>();
            return c != null ? c : gameObject.AddComponent<T>();
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 1f / 20f);
            if (dt <= 0f) return;

            if (aimCamera == null) aimCamera = Camera.main;
            CursorWorld = PrototypeInput.MouseWorld(aimCamera);
            Vector2 toCursor = CursorWorld - _position;
            if (toCursor.sqrMagnitude > 1e-6f) AimDirection = toCursor.normalized;

            if (PrototypeInput.JumpPressed) _jumpBuffer = tuning.jumpBufferTime + 1e-4f;
            else _jumpBuffer -= dt;

            grapple.Tick(this, dt);

            if (State == PlayerMovementState.Attached) UpdateAttached(dt);
            else UpdateAirborne(dt);

            if (_position.y < killY) Respawn();

            transform.position = new Vector3(_position.x, _position.y, transform.position.z);
        }

        // ---------------------------------------------------------------- attached

        void UpdateAttached(float dt)
        {
            if (_surface == null || !_surface.isActiveAndEnabled)
            {
                EnterAirborne(Vector2.zero, null);
                return;
            }

            Vector2 prevPos = _position;
            float r = tuning.playerRadius;
            SurfaceSample cur = _surface.SampleAt(_pathPos);

            // How far the surface itself carried us since last frame (same path position, new transform).
            Vector2 carried = (cur.point + cur.normal * r) - (_sample.point + _sample.normal * r);
            _platformVelocity = carried / dt;

            // --- input -> signed path speed
            int sign = inputResolver.Resolve(PrototypeInput.MoveVector, PrototypeInput.MoveKeyPressedThisFrame,
                cur.tangent, _smoothedNormal, tuning);
            float target = sign * tuning.surfaceMoveSpeed;
            bool reversing = sign != 0 && Mathf.Abs(_surfaceSpeed) > 0.01f && Mathf.Sign(_surfaceSpeed) != sign;
            float rate = sign != 0 && !reversing ? tuning.surfaceAcceleration : Mathf.Max(tuning.surfaceDeceleration, tuning.surfaceAcceleration);
            _surfaceSpeed = rate <= 0f ? target : Mathf.MoveTowards(_surfaceSpeed, target, rate * dt);

            // --- advance along the path
            if (Mathf.Abs(_surfaceSpeed) > 1e-4f)
            {
                float ds = _surfaceSpeed * dt;
                Vector2 curCenter = cur.point + cur.normal * r;
                float newPos = _pathPos + ds;
                SurfaceSample next = _surface.SampleAt(newPos);

                if (tuning.compensateCurvature)
                {
                    float moved = ((next.point + next.normal * r) - curCenter).magnitude;
                    if (moved > 1e-5f)
                    {
                        newPos = _pathPos + ds * Mathf.Clamp(Mathf.Abs(ds) / moved, 0.2f, 5f);
                        next = _surface.SampleAt(newPos);
                    }
                }

                Vector2 nextCenter = next.point + next.normal * r;
                if (tuning.allowSurfaceTransfer && TryTransfer(cur, curCenter, nextCenter))
                    return;

                _pathPos = _surface.WrapPathPosition(newPos);
                cur = next;
            }

            _sample = cur;
            UpdateNormals(dt, false);

            // --- constrain the root to the surface (with a decaying blend after attaching)
            _attachOffset *= Mathf.Exp(-tuning.surfaceSnapStrength * dt);
            _position = cur.point + cur.normal * r + _attachOffset;
            _velocity = (_position - prevPos) / dt;

            // --- jump preview & jump
            JumpResult preview = jumpResolver.Resolve(BuildJumpContext(), tuning);
            if (_jumpBuffer > 0f) Jump(preview);
        }

        bool TryTransfer(in SurfaceSample cur, Vector2 curCenter, Vector2 nextCenter)
        {
            Vector2 moveDir = nextCenter - curCenter;
            if (moveDir.sqrMagnitude < 1e-10f) return false;
            moveDir.Normalize();

            var candidates = sensor.Refresh(nextCenter, tuning.playerRadius, tuning.surfaceTransferDistance);
            int best = -1;
            float bestGap = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                SurfaceCandidate c = candidates[i];
                if (c.Surface == _surface || !c.Surface.Attachable) continue;
                if (Vector2.Dot(moveDir, -c.sample.separation) < 0.2f) continue; // not crawling into it
                if (c.gap < bestGap) { bestGap = c.gap; best = i; }
            }
            if (best < 0) return false;

            Vector2 oldNormal = cur.normal;
            AttachTo(candidates[best].sample, true, moveDir, oldNormal);
            return true;
        }

        void UpdateNormals(float dt, bool snap)
        {
            _surface.GetChordFrame(_pathPos, tuning.normalSampleSpacing, out _, out Vector2 chordNormal,
                out _footBack, out _footFront);

            if (snap || tuning.normalSmoothing <= 0f)
                _smoothedNormal = chordNormal;
            else
            {
                float k = 1f - Mathf.Exp(-dt / tuning.normalSmoothing);
                _smoothedNormal = Surface2D.Rotate(_smoothedNormal, Vector2.SignedAngle(_smoothedNormal, chordNormal) * k).normalized;
            }
        }

        JumpContext BuildJumpContext() => new JumpContext
        {
            position = _position,
            radius = tuning.playerRadius,
            surfaceSample = _sample,
            localNormal = _sample.normal,
            smoothedNormal = _smoothedNormal,
            cursorWorld = CursorWorld,
            jumpSpeed = tuning.jumpSpeed,
            gravity = tuning.gravity,
        };

        void Jump(JumpResult jr)
        {
            _jumpBuffer = 0f;
            Vector2 v = jr.direction * tuning.jumpSpeed + _sample.tangent * (_surfaceSpeed * tuning.inheritSurfaceVelocity)
                        + _platformVelocity * tuning.inheritPlatformVelocity;

            LastJumpDirection = jr.direction;
            LastJumpResult = jr;
            _position += _sample.normal * 0.01f;
            LastJumpOrigin = _position;
            EnterAirborne(v, _surface);
        }

        // ---------------------------------------------------------------- airborne

        void UpdateAirborne(float dt)
        {
            float r = tuning.playerRadius;

            if (_jumpBuffer > 0f) TryAirJump();

            if (!grapple.SuppressesGravity(tuning))
                _velocity += Vector2.down * (tuning.gravity * dt);

            float ix = PrototypeInput.MoveVector.x; // air control is horizontal only
            if (tuning.airControl > 0f && ix != 0f)
            {
                float airScale = tuning.airControlSpeedScale.Evaluate(_velocity);
                float targetX = ix * tuning.airMoveSpeed * airScale;
                bool alreadyFaster = Mathf.Sign(_velocity.x) == Mathf.Sign(ix) && Mathf.Abs(_velocity.x) >= Mathf.Abs(targetX);
                if (!alreadyFaster) // steering never brakes momentum in the held direction
                    _velocity.x = Mathf.MoveTowards(_velocity.x, targetX,
                        tuning.airControl * tuning.airAcceleration * airScale * dt);
            }

            grapple.ApplyAirborneVelocity(ref _velocity, _position, dt, tuning);

            if (tuning.attachmentMode == AttachmentMode.Magnetic && attachment.MagnetTarget is SurfaceCandidate m)
            {
                float k = 1f - Mathf.Clamp01(m.gap / Mathf.Max(0.01f, attachment.CurrentMagnetRange));
                _velocity += -m.sample.separation * (tuning.magnetStrength * k * dt);
            }

            if (_velocity.y < -tuning.maxFallSpeed) _velocity.y = -tuning.maxFallSpeed;

            float dist = _velocity.magnitude * dt;
            int steps = Mathf.Clamp(Mathf.CeilToInt(dist / (r * 0.25f)), 1, 32);
            float h = dt / steps;

            for (int i = 0; i < steps; i++)
            {
                _position += _velocity * h;
                grapple.ConstrainSubstep(ref _position, ref _velocity, tuning);

                var candidates = sensor.Refresh(_position, r, tuning.SensorRange);
                if (attachment.Evaluate(candidates, _position, _velocity, AimDirection, LastJumpDirection, tuning,
                        out SurfaceCandidate chosen))
                {
                    AttachTo(chosen.sample, false, Vector2.zero, Vector2.zero);
                    return;
                }

                ResolvePenetrations(candidates);
            }
        }

        /// <summary>
        /// Jump pressed while airborne. Releases a latched grapple, then spends an air jump if one is
        /// available. If a landing is imminent, the press is left buffered so it becomes a ground jump.
        /// </summary>
        void TryAirJump()
        {
            bool releasedGrapple = false;
            if (grapple.IsLatched)
            {
                grapple.Release();
                releasedGrapple = true;
            }

            bool canAirJump = tuning.enableAirJumps && _airJumpsUsed < tuning.airJumpCount &&
                              TimeSinceDetach >= tuning.airJumpMinDelay;
            if (canAirJump && !LandingImminent())
            {
                _jumpBuffer = 0f;
                _airJumpsUsed++;
                Vector2 dir = jumpResolver.ResolveAirJump(PrototypeInput.MoveVector, AimDirection, tuning);
                // Replace vertical velocity (consistent height even when falling fast); keep some horizontal momentum.
                _velocity = dir * tuning.airJumpSpeed + new Vector2(_velocity.x * tuning.airJumpMomentumKeep, 0f);
                LastJumpDirection = dir;
                LastJumpOrigin = _position;
                AirJumped?.Invoke(dir);
            }
            else if (releasedGrapple)
            {
                _jumpBuffer = 0f; // the press was spent letting go
            }
        }

        bool LandingImminent()
        {
            if (tuning.airJumpLandingGrace <= 0f) return false;
            foreach (SurfaceCandidate c in sensor.Candidates)
                if (!c.blocked && c.approachSpeed > 0f && c.gap <= tuning.airJumpLandingGrace && c.Surface.Attachable)
                    return true;
            return false;
        }

        void OnGrappleLatched(GrappleController g)
        {
            if (tuning.grappleLatchRefreshesAirJumps) _airJumpsUsed = 0;
            if (State == PlayerMovementState.Attached)
                EnterAirborne(Vector2.zero, _surface); // pulled/swung off the current surface
        }

        void ResolvePenetrations(System.Collections.Generic.List<SurfaceCandidate> candidates)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                SurfaceCandidate c = candidates[i];
                if (c.gap >= 0f) continue;
                Vector2 n = c.sample.separation;
                _position += n * -c.gap;
                float vn = Vector2.Dot(_velocity, n);
                if (vn < 0f) _velocity -= n * vn;
            }
        }

        // ---------------------------------------------------------------- transitions

        void AttachTo(SurfaceSample hit, bool isTransfer, Vector2 moveDir, Vector2 oldNormal)
        {
            float r = tuning.playerRadius;
            _surface = hit.surface;
            _pathPos = hit.pathPosition;
            _sample = _surface.SampleAt(_pathPos);

            Vector2 desired = _sample.point + _sample.normal * r;
            _attachOffset = Vector2.ClampMagnitude(_position - desired, r * 2f);

            if (isTransfer)
            {
                // Continue away from the surface we came from (e.g. floor -> up the wall).
                float score = Vector2.Dot(_sample.tangent, moveDir) + Vector2.Dot(_sample.tangent, oldNormal);
                int newSign = score >= 0f ? 1 : -1;
                _surfaceSpeed = Mathf.Abs(_surfaceSpeed) * newSign;
                inputResolver.RemapLocks(newSign);
                UpdateNormals(0f, false);
            }
            else
            {
                _surfaceSpeed = 0f;
                inputResolver.ClearLocks();
                UpdateNormals(0f, true);
                _attachTime = Time.time;
                _airJumpsUsed = 0;
                grapple.NotifyPlayerAttached();
            }

            State = PlayerMovementState.Attached;
            _velocity = Vector2.zero;
            _platformVelocity = Vector2.zero;
            attachment.NotifyAttached();
            Attached?.Invoke(_surface, isTransfer);
        }

        void EnterAirborne(Vector2 velocity, Surface2D leftSurface)
        {
            State = PlayerMovementState.Airborne;
            _velocity = velocity;
            _surfaceSpeed = 0f;
            _attachOffset = Vector2.zero;
            _detachTime = Time.time;
            _surface = null;
            attachment.NotifyDetached(leftSurface);
            inputResolver.ClearLocks();
            Detached?.Invoke(leftSurface);
        }

        public void TeleportTo(Vector2 position)
        {
            _position = position;
            _airJumpsUsed = 0;
            if (grapple != null) grapple.ResetImmediate();
            EnterAirborne(Vector2.zero, null);
            transform.position = new Vector3(_position.x, _position.y, transform.position.z);
        }

        public void Respawn()
        {
            TeleportTo(SpawnPosition);
            Respawned?.Invoke();
        }

        /// <summary>
        /// Load a complete tuning into the runtime copy (turns persistence off so the asset is never touched).
        /// Used by the playtest session to switch configurations.
        /// </summary>
        public void LoadRuntimeTuning(MovementTuning source)
        {
            SetPersistTuning(false);
            CopyTuning(source, tuning);
        }
    }
}
