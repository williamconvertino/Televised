using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Televised.Prototyping.Movement
{
    /// <summary>Sandbox shortcuts: R respawn, 1-9 jump to spawn points, T teleport to cursor.</summary>
    public class SandboxSpawner : MonoBehaviour
    {
        [SerializeField] PlayerMotor2D motor;
        [SerializeField] List<Transform> spawnPoints = new List<Transform>();

        static readonly Key[] NumberKeys =
            { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };

        void Start()
        {
            if (motor != null && spawnPoints.Count > 0 && spawnPoints[0] != null)
            {
                motor.SpawnPosition = spawnPoints[0].position;
                motor.Respawn();
            }
        }

        void Update()
        {
            if (motor == null) return;

            for (int i = 0; i < NumberKeys.Length && i < spawnPoints.Count; i++)
            {
                if (!PrototypeInput.Pressed(NumberKeys[i]) || spawnPoints[i] == null) continue;
                motor.SpawnPosition = spawnPoints[i].position;
                motor.Respawn();
            }

            if (PrototypeInput.Pressed(Key.R)) motor.Respawn();
            if (PrototypeInput.Pressed(Key.T)) motor.TeleportTo(motor.CursorWorld);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.8f);
            for (int i = 0; i < spawnPoints.Count; i++)
                if (spawnPoints[i] != null) Gizmos.DrawWireSphere(spawnPoints[i].position, 0.35f);
        }
    }
}
