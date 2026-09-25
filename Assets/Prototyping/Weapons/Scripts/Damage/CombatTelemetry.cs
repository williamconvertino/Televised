using System.Collections.Generic;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// Session statistics for tuning comparisons. Counts only; don't over-interpret them.
    /// Weapons report activations / shots; damage, kills and hits are collected from health events.
    /// </summary>
    public static class CombatTelemetry
    {
        public class WeaponStats
        {
            public int activations;
            /// <summary>Projectiles fired (projectile weapons) - denominator for accuracy.</summary>
            public int shots;
            /// <summary>Projectiles that hit at least one enemy.</summary>
            public int shotsThatHit;
            /// <summary>Individual damage applications (every tick of a beam counts).</summary>
            public int hits;
            public float damage;
            public int kills;
            /// <summary>Wall-slam damage caused by enemies this weapon launched.</summary>
            public float slamDamage;

            public string Accuracy => shots > 0 ? $"{100f * shotsThatHit / shots:0}%" : "-";
        }

        public static float DamageDealt;
        public static float DamageTaken;
        public static int Kills;
        public static int BodyImpactHits;
        public static float BodyImpactDamage;
        public static float SelfDamage;
        public static readonly Dictionary<string, WeaponStats> ByWeapon = new Dictionary<string, WeaponStats>();

        static readonly Queue<(float time, float dmg)> s_recent = new Queue<(float, float)>();
        public const float DpsWindow = 3f;

        public static WeaponStats For(string weapon)
        {
            if (string.IsNullOrEmpty(weapon)) weapon = "?";
            if (!ByWeapon.TryGetValue(weapon, out WeaponStats s)) ByWeapon[weapon] = s = new WeaponStats();
            return s;
        }

        public static void Activation(string weapon) => For(weapon).activations++;
        public static void Shot(string weapon) => For(weapon).shots++;
        public static void ShotHit(string weapon) => For(weapon).shotsThatHit++;

        public static void EnemyDamaged(in DamageEvent e)
        {
            if (e.blocked) return;
            DamageDealt += e.finalDamage;
            s_recent.Enqueue((Time.time, e.finalDamage));
            WeaponStats s = For(e.sourceName);
            s.hits++;
            s.damage += e.finalDamage;
            if (e.kind == DamageKind.WallSlam && !string.IsNullOrEmpty(e.cause)) For(e.cause).slamDamage += e.finalDamage;
            if (e.kind == DamageKind.BodyImpact)
            {
                BodyImpactHits++;
                BodyImpactDamage += e.finalDamage;
            }
            if (e.killed)
            {
                Kills++;
                s.kills++;
            }
        }

        public static void PlayerDamaged(in DamageEvent e)
        {
            DamageTaken += e.finalDamage;
            if (e.kind == DamageKind.ImpactSelfDamage) SelfDamage += e.finalDamage;
        }

        /// <summary>Damage dealt per second over the last few seconds.</summary>
        public static float RecentDps
        {
            get
            {
                while (s_recent.Count > 0 && Time.time - s_recent.Peek().time > DpsWindow) s_recent.Dequeue();
                float sum = 0f;
                foreach (var r in s_recent) sum += r.dmg;
                return sum / DpsWindow;
            }
        }

        public static void Reset()
        {
            DamageDealt = DamageTaken = BodyImpactDamage = SelfDamage = 0f;
            Kills = BodyImpactHits = 0;
            ByWeapon.Clear();
            s_recent.Clear();
        }
    }
}
