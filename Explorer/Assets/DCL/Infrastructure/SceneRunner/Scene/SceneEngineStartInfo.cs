using System;

namespace SceneRunner.Scene
{
    /// <summary>
    ///     Information about the engine upon the scene launch
    /// </summary>
    public readonly struct SceneEngineStartInfo
    {
        /// <summary>
        ///     We must use <see cref="DateTime" /> as unity API is not thread-safe
        /// </summary>
        public readonly DateTime Timestamp;
        public readonly int FrameNumber;

        public SceneEngineStartInfo(DateTime timestamp, int frameNumber)
        {
            Timestamp = timestamp;
            FrameNumber = frameNumber;
        }
    }
}
