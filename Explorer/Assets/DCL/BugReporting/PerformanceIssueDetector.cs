namespace DCL.BugReporting
{
    /// <summary>
    ///     Decides when frame times degraded enough to offer the user a bug report: either one
    ///     frame long enough to read as a freeze, or a sustained low average frame rate over a
    ///     rolling window.
    /// </summary>
    public class PerformanceIssueDetector
    {
        private readonly float hiccupSeconds;
        private readonly float lowFpsThreshold;
        private readonly float windowSeconds;

        private float windowElapsed;
        private int windowFrames;

        public PerformanceIssueDetector(float hiccupSeconds, float lowFpsThreshold, float windowSeconds)
        {
            this.hiccupSeconds = hiccupSeconds;
            this.lowFpsThreshold = lowFpsThreshold;
            this.windowSeconds = windowSeconds;
        }

        /// <summary>Feeds one frame. True when this frame completes the evidence of an issue.</summary>
        public bool OnFrame(float deltaSeconds, out PerformanceIssue issue)
        {
            if (deltaSeconds >= hiccupSeconds)
            {
                // The hiccup itself would drag the window's average down, double reporting one event.
                Reset();
                issue = PerformanceIssue.Hiccup(deltaSeconds);
                return true;
            }

            windowElapsed += deltaSeconds;
            windowFrames++;

            if (windowElapsed < windowSeconds)
            {
                issue = default;
                return false;
            }

            float averageFps = windowFrames / windowElapsed;
            Reset();

            if (averageFps < lowFpsThreshold)
            {
                issue = PerformanceIssue.LowFps(averageFps);
                return true;
            }

            issue = default;
            return false;
        }

        /// <summary>
        ///     Discards the rolling window. Call it when detection was paused, so the frames that
        ///     accumulated before the pause are not mixed with a window measured after it.
        /// </summary>
        public void Reset()
        {
            windowElapsed = 0f;
            windowFrames = 0;
        }
    }

    public readonly struct PerformanceIssue
    {
        public readonly bool IsHiccup;

        /// <summary>The hiccup duration in seconds, or the window's average FPS.</summary>
        public readonly float Value;

        private PerformanceIssue(bool isHiccup, float value)
        {
            IsHiccup = isHiccup;
            Value = value;
        }

        public static PerformanceIssue Hiccup(float durationSeconds) =>
            new (true, durationSeconds);

        public static PerformanceIssue LowFps(float averageFps) =>
            new (false, averageFps);
    }
}
