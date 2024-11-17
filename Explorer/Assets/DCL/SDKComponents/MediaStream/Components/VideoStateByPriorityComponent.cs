using Arch.Core;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    /// Data related to the calculation of the priority of a video and its corresponding state (playing or paused).
    /// </summary>
    public struct VideoStateByPriorityComponent
    {
        /// <summary>
        /// The final score used to determine the priority of the video.
        /// </summary>
        public float Score;

        /// <summary>
        /// Whether the video should be playing or not, if prioritization mechanism did not exist.
        /// </summary>
        public bool WantsToPlay;

        /// <summary>
        /// The entity that has the MediaPlayer.
        /// </summary>
        public Entity Entity;

        /// <summary>
        /// Half of the size of the mesh renderer (or mesh renderer group that consume the same video texture).
        /// </summary>
        public float HalfSize;

        /// <summary>
        /// Whether the video should be playing according to its priority.
        /// </summary>
        public bool IsPlaying;

        /// <summary>
        /// The time when the video was played manually.
        /// </summary>
        public float MediaPlayStartTime;

        /// <summary>
        /// A mesh renderer used for visually debugging the priority of each video.
        /// </summary>
        public MeshRenderer DebugPrioritySign;
    }
}
