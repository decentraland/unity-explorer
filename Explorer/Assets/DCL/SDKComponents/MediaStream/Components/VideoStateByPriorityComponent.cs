using Arch.Core;
using DCL.ECSComponents;

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

        public float Size;

        public float LastTimePaused;

        public bool IsPlaying;

        public float MediaPlayStartTime;
    }
}
