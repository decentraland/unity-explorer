namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     The process-wide minimum gap between two media opens, shared by scene video players and cast client videos. Main-thread only, not
    ///     thread-safe.
    /// </summary>
    internal static class MediaOpenThrottle
    {
        private const float MIN_OPEN_INTERVAL_SECONDS = 0.5f;

        private static float lastOpenedAt;

        /// <summary>
        ///     Whether at least half a second passed since the last <see cref="MarkOpened" />.
        /// </summary>
        internal static bool CanOpen(float now) =>
            SecondsSinceLastOpen(now) >= MIN_OPEN_INTERVAL_SECONDS;

        /// <summary>
        ///     The seconds from the last <see cref="MarkOpened" />, or from startup before the first one, to <paramref name="now" />.
        /// </summary>
        internal static float SecondsSinceLastOpen(float now) =>
            now - lastOpenedAt;

        /// <summary>
        ///     Records an open at <paramref name="now" />.
        /// </summary>
        internal static void MarkOpened(float now)
        {
            lastOpenedAt = now;
        }
    }
}
