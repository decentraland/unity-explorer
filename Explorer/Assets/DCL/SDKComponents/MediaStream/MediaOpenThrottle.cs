namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     The process-wide minimum gap between two media opens, shared by scene video players and cast client videos. Main-thread only, not
    ///     thread-safe.
    /// </summary>
    internal static class MediaOpenThrottle
    {
        private const float MIN_OPEN_INTERVAL_SECONDS = 0.5f;

        private static float lastOpenedAt = float.NegativeInfinity;

        /// <summary>
        ///     Whether at least half a second passed since the last <see cref="MarkOpened" />.
        /// </summary>
        internal static bool CanOpen(float now) =>
            now - lastOpenedAt >= MIN_OPEN_INTERVAL_SECONDS;

        /// <summary>
        ///     Records an open at <paramref name="now" />.
        /// </summary>
        internal static void MarkOpened(float now)
        {
            lastOpenedAt = now;
        }
    }
}
