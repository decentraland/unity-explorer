namespace UUAV
{
    /// <summary>
    /// Pure-math mirror of uuav-ipc's native media_time_of - keep both in sync.
    /// </summary>
    public static class AudioMasterClock
    {
        /// <summary>
        /// basePts + (framesConsumed - baseFrames) * rate / sampleRate; clamps rather than running backwards or dividing by a bad sample rate.
        /// </summary>
        public static double MediaTime(
            double basePts,
            long framesConsumed,
            long baseFrames,
            double rate,
            int sampleRate
        )
        {
            if (sampleRate <= 0)
            {
                return basePts;
            }

            long advanced = framesConsumed - baseFrames;
            if (advanced < 0)
            {
                advanced = 0;
            }

            return basePts + advanced * rate / sampleRate;
        }
    }
}
