using Arch.Core;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///
    /// </summary>
    public struct VideoStateByPriorityComponent
    {
        /// <summary>
        ///
        /// </summary>
        public float Score;

        /// <summary>
        ///
        /// </summary>
        public bool WantsToPlay;

        /// <summary>
        ///
        /// </summary>
        public Entity Entity;

        /// <summary>
        ///
        /// </summary>
        public float Size;

        /// <summary>
        ///
        /// </summary>
        public bool IsPlaying;

        /// <summary>
        ///
        /// </summary>
        public float MediaPlayStartTime;

        /// <summary>
        ///
        /// </summary>
        public MeshRenderer DebugPrioritySign;
    }
}
