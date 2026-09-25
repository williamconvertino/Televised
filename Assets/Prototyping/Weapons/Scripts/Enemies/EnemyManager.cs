using System.Collections.Generic;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// Global enemy controls for the sandbox: respawn / kill / reset, auto respawn, freeze, global health / contact /
    /// knockback multipliers, and the movement override. Also owns the list of spawn groups.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public class EnemyManager : MonoBehaviour
    {
        public static EnemyManager Instance { get; private set; }

        public bool autoRespawn = true;
        [Min(0f)] public float respawnDelay = 2f;
        [Tooltip("Stops AI movement (knockback still applies; use the knockback multiplier to disable it).")]
        public bool freezeEnemies;
        [Min(0.01f)] public float healthMultiplier = 1f;
        [Min(0f)] public float contactDamageMultiplier = 1f;
        [Min(0f)] public float knockbackMultiplier = 1f;
        public EnemyMovementOverride movementOverride = EnemyMovementOverride.Individual;

        [Header("Wall slam")]
        [Tooltip("Enemies launched into terrain (knockback, chain) take damage from their speed into the surface.")]
        public bool wallSlamEnabled = true;
        [Min(0f)] public float wallSlamMinSpeed = 5f;
        [Min(0f)] public float wallSlamBaseDamage = 6f;
        [Tooltip("Damage per unit of speed into the surface above the minimum.")]
        [Min(0f)] public float wallSlamDamagePerSpeed = 3f;
        [Min(0f)] public float wallSlamMaxDamage = 60f;
        [Tooltip("How much of the speed into the surface bounces back (0 = splat, 1 = full bounce).")]
        [Range(0f, 1f)] public float wallSlamRestitution = 0.5f;
        [Min(0f)] public float wallSlamCooldown = 0.25f;

        readonly List<EnemySpawnGroup> _groups = new List<EnemySpawnGroup>();
        public IReadOnlyList<EnemySpawnGroup> Groups => _groups;

        bool _lastAutoRespawn;

        void Awake()
        {
            Instance = this;
            _lastAutoRespawn = autoRespawn;
        }

        void Start()
        {
            _groups.AddRange(FindObjectsByType<EnemySpawnGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            _groups.Sort((a, b) => string.CompareOrdinal(HierarchyPath(a.transform), HierarchyPath(b.transform)));
        }

        static string HierarchyPath(Transform t)
        {
            string p = t.GetSiblingIndex().ToString("D3");
            for (Transform x = t.parent; x != null; x = x.parent) p = x.GetSiblingIndex().ToString("D3") + "/" + p;
            return p;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (_lastAutoRespawn != autoRespawn)
            {
                _lastAutoRespawn = autoRespawn;
                RefreshRespawnTimers();
            }
        }

        public void RefreshRespawnTimers()
        {
            foreach (DummyEnemy e in DummyEnemy.All) e.RefreshRespawnTimer();
        }

        public void RespawnAll()
        {
            foreach (DummyEnemy e in DummyEnemy.All) if (e.gameObject.activeSelf) e.Respawn();
        }

        /// <summary>Respawn only the dead ones.</summary>
        public void RespawnDead()
        {
            foreach (DummyEnemy e in DummyEnemy.All) if (e.gameObject.activeSelf && !e.IsAlive) e.Respawn();
        }

        public void KillAll()
        {
            foreach (DummyEnemy e in DummyEnemy.All) if (e.IsAlive && e.gameObject.activeSelf) e.Kill();
        }

        /// <summary>Respawn everything at its spawn point and put the global settings back to defaults.</summary>
        public void ResetAll(bool resetSettings)
        {
            if (resetSettings)
            {
                autoRespawn = true;
                respawnDelay = 2f;
                freezeEnemies = false;
                healthMultiplier = contactDamageMultiplier = knockbackMultiplier = 1f;
                movementOverride = EnemyMovementOverride.Individual;
                foreach (EnemySpawnGroup g in _groups)
                {
                    g.autoRespawn = GroupAutoRespawn.UseGlobal;
                    g.movementOverride = EnemyMovementOverride.Individual;
                    g.GroupEnabled = true;
                }
            }
            RespawnAll();
        }

        public int AliveCount
        {
            get
            {
                int n = 0;
                foreach (DummyEnemy e in DummyEnemy.All) if (e.IsTargetable) n++;
                return n;
            }
        }
    }
}
