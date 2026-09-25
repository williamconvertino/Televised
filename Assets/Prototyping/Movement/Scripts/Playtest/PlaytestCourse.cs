using System.Collections.Generic;
using UnityEngine;

namespace Televised.Prototyping.Movement.Playtest
{
    public enum PlaytestDifficulty
    {
        Easy,
        Medium,
        Hard
    }

    /// <summary>Which set of courses: the general course, or one built to require a feature.</summary>
    public enum CourseTrack
    {
        General,
        DoubleJump,
        Grapple
    }

    /// <summary>
    /// One playable course in the playtest scene: start point, ordered checkpoints and a goal.
    /// Several courses (one per difficulty) live side by side in the same scene.
    /// </summary>
    public class PlaytestCourse : MonoBehaviour
    {
        public PlaytestDifficulty difficulty;
        public CourseTrack track;
        public Transform startPoint;
        [Tooltip("Ordered checkpoints (not including the goal).")]
        public List<PlaytestCheckpoint> checkpoints = new List<PlaytestCheckpoint>();
        public PlaytestCheckpoint goal;

        PlaytestHazard[] _hazards;

        public PlaytestHazard[] Hazards => _hazards ??= GetComponentsInChildren<PlaytestHazard>();

        public void ResetCheckpoints()
        {
            foreach (PlaytestCheckpoint cp in checkpoints)
                if (cp != null) cp.SetReached(false);
        }
    }
}
