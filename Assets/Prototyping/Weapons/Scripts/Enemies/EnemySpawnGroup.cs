using System.Collections.Generic;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    public enum GroupAutoRespawn
    {
        UseGlobal,
        On,
        Off,
    }

    /// <summary>
    /// A logical set of dummies (RangeTargets, PiercingLine, DenseCluster...) that can be enabled, respawned and
    /// overridden together from the ENEMIES tab. Enemies are the DummyEnemy children of this object.
    /// </summary>
    public class EnemySpawnGroup : MonoBehaviour
    {
        [SerializeField] string displayName;
        [TextArea] [SerializeField] string description;
        public GroupAutoRespawn autoRespawn = GroupAutoRespawn.UseGlobal;
        public EnemyMovementOverride movementOverride = EnemyMovementOverride.Individual;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public string Description => description;

        readonly List<DummyEnemy> _members = new List<DummyEnemy>();
        bool _collected;

        public IReadOnlyList<DummyEnemy> Members
        {
            get
            {
                if (!_collected)
                {
                    GetComponentsInChildren(true, _members);
                    _collected = true;
                }
                return _members;
            }
        }

        public bool GroupEnabled
        {
            get
            {
                foreach (DummyEnemy e in Members) if (e.gameObject.activeSelf) return true;
                return false;
            }
            set
            {
                foreach (DummyEnemy e in Members)
                {
                    if (e.gameObject.activeSelf == value) continue;
                    e.gameObject.SetActive(value);
                    if (value) e.Respawn();
                }
            }
        }

        public int AliveCount
        {
            get
            {
                int n = 0;
                foreach (DummyEnemy e in Members) if (e.IsTargetable) n++;
                return n;
            }
        }

        public bool ResolveAutoRespawn(bool global) =>
            autoRespawn == GroupAutoRespawn.On || (autoRespawn == GroupAutoRespawn.UseGlobal && global);

        public void RespawnAll()
        {
            foreach (DummyEnemy e in Members) if (e.gameObject.activeSelf) e.Respawn();
        }

        public void KillAll()
        {
            foreach (DummyEnemy e in Members) if (e.gameObject.activeSelf && e.IsAlive) e.Kill();
        }
    }
}
