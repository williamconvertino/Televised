using System;
using System.Collections.Generic;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// Weapon 1: a charged wide beam from the eye toward the cursor. Charge, then a short, heavy blast that hits
    /// everything along it. Terrain cuts off the parts of the beam it covers. Movement lock is optional (off by
    /// default), so you can keep crawling and jumping while it charges.
    /// </summary>
    public class EyeBeamWeapon : ChargedBeamWeapon
    {
        [Serializable]
        public class EyeBeamSettings : ChargedBeamSettings
        {
            [Tooltip("The beam keeps following the cursor while it fires (off = it stays where it was aimed).")]
            public bool trackCursorWhileFiring = true;

            public EyeBeamSettings()
            {
                damage = 55f;
                chargeDuration = 0.3f;
                activeDuration = 0.25f;
                recoveryDuration = 0.12f;
                beamWidth = 1f;
                extent = new RangeSettings(16f, 0.75f);
                knockback = new KnockbackSettings(5f, KnockbackDirection.AlongAttackDirection);
                color = new Color(0.45f, 0.95f, 1f, 0.85f);
            }
        }

        [SerializeField] EyeBeamSettings settings = new EyeBeamSettings();

        public override string DisplayName => "Eye Beam";
        public override string Summary => "Charge, then a wide beam toward the cursor. Terrain splits it.";
        public override object Settings => settings;
        protected override ChargedBeamSettings Beam => settings;

        Vector2 _aim = Vector2.right;

        protected override void OnChargeStart() => _aim = C.AimDirection;

        protected override void TrackAim()
        {
            if (CurrentState == State.Charging || (CurrentState == State.Active && settings.trackCursorWhileFiring))
                _aim = C.AimDirection;
        }

        protected override void GetArms(List<(Vector2 origin, Vector2 dir)> arms) => arms.Add((C.Origin, _aim));

        public override void DrawTuning(TuningGui g)
        {
            g.Header("Aim");
            settings.trackCursorWhileFiring = g.Toggle("Follow cursor while firing", settings.trackCursorWhileFiring);
            DrawBeamTuning(g);
        }
    }
}
