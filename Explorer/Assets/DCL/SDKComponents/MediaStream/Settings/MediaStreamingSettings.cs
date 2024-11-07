using UnityEngine;

namespace DCL.SDKComponents.MediaStream.Settings
{
    /// <summary>
    ///
    /// </summary>
    [CreateAssetMenu(fileName = "VideoStreamingSettings", menuName = "SO/VideoStreamingSettings", order = 0)]
    public class VideoStreamingSettings : ScriptableObject
    {
        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public int MaxSimultaneousVideoStreams = 10;
    }
}
