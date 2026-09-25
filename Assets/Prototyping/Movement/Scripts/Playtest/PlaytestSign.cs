using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Movement.Playtest
{
    /// <summary>A world-space text label drawn by the playtest HUD (section names, tips).</summary>
    public class PlaytestSign : MonoBehaviour
    {
        [TextArea] public string text;
    }
}
