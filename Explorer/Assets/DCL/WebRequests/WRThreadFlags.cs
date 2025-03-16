using System;

namespace DCL.WebRequests
{
    [Flags]

    // ReSharper disable once InconsistentNaming
    public enum WRThreadFlags
    {
        /// <summary>
        ///     Switch to ThreadPool for deserialization
        /// </summary>
        SwitchToThreadPool = 1,

        /// <summary>
        ///     Switch back to MainThread after deserialization
        /// </summary>
        SwitchBackToMainThread = 1 << 1,

        /// <summary>
        ///     Switch to the thread pool and back
        /// </summary>
        SwitchToThreadPoolAndBack = SwitchToThreadPool | SwitchBackToMainThread
    }
}
