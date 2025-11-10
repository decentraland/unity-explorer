using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    ///
    /// </summary>
    public readonly struct MoveBeforePlayingSocialEmoteIntent
    {
        /// <summary>
        ///
        /// </summary>
        public readonly Vector3 InitiatorPosition;

        /// <summary>
        ///
        /// </summary>
        public readonly float MovementStartTime;

        public MoveBeforePlayingSocialEmoteIntent(Vector3 initiatorPosition)
        {
            InitiatorPosition = initiatorPosition;
            MovementStartTime = Time.time;
        }
    }
}
