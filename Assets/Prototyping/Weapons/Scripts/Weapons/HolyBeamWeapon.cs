using System;
using System.Collections.Generic;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    public enum HolyBeamMode
    {
        Vertical,
        Horizontal,
        Cross,
    }

    /// <summary>
    /// Weapons 6 and 7: world-aligned beams out of the eye (up + down, left + right, or both as a cross).
    /// A positioning weapon: charge, lock in place, fire. Each arm is a lane beam, so surfaces contain it and split
    /// it; you can't hit through a floor or wall. Optionally grants an air jump afterwards (jump-beam-jump).
    /// The same component serves as "Holy Beam" and "Cross Holy Beam" with a different default mode.
    /// </summary>
    public class HolyBeamWeapon : ChargedBeamWeapon
    {
        [Serializable]
        public class HolySettings : ChargedBeamSettings
        {
            public HolyBeamMode mode = HolyBeamMode.Vertical;
            [Tooltip("When movement isn't locked, the beam follows the player instead of staying where it started.")]
            public bool followPlayer = true;

            public HolySettings()
            {
                damage = 70f;
                chargeDuration = 0.3f;
                activeDuration = 0.3f;
                recoveryDuration = 0.2f;
                cooldown = 0.5f;
                refireWhileHeld = false;
                beamWidth = 1.8f;
                extent = new RangeSettings(18f, 0.7f);
                lockDuringCharge = true;
                lockDuringActive = true;
                lockDuringRecovery = true;
                airJumpAfterBeam = true;
                knockback = new KnockbackSettings(7f, KnockbackDirection.AlongAttackDirection);
                color = new Color(1f, 0.93f, 0.55f, 0.85f);
            }
        }

        [SerializeField] string displayName = "Holy Beam";
        [SerializeField] HolySettings settings = new HolySettings();

        public override string DisplayName => displayName;
        public override string Summary => settings.mode == HolyBeamMode.Cross
            ? "Charge, lock in place, then + shaped beams out of the eye. Surfaces contain them."
            : $"Charge, lock in place, then {settings.mode.ToString().ToLower()} beams out of the eye. Surfaces contain them.";
        public override object Settings => settings;
        protected override ChargedBeamSettings Beam => settings;

        Vector2 _center;

        protected override void OnChargeStart() => _center = C.Origin;

        protected override void TrackAim()
        {
            if (!MovementLocked && settings.followPlayer) _center = C.Origin;
        }

        protected override void GetArms(List<(Vector2 origin, Vector2 dir)> arms)
        {
            if (settings.mode != HolyBeamMode.Horizontal)
            {
                arms.Add((_center, Vector2.up));
                arms.Add((_center, Vector2.down));
            }
            if (settings.mode != HolyBeamMode.Vertical)
            {
                arms.Add((_center, Vector2.right));
                arms.Add((_center, Vector2.left));
            }
        }

        public override void DrawTuning(TuningGui g)
        {
            g.Header("General");
            settings.mode = g.Enum("Mode", settings.mode);
            settings.followPlayer = g.Toggle("Beam follows player when unlocked", settings.followPlayer);
            DrawBeamTuning(g);
        }
    }
}
