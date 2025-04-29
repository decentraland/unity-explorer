using UnityEngine;

namespace VirtualTexture
{
    /// <summary>
    /// Utility class for measuring and aggregating per-frame performance metrics.
    /// Tracks timing information for profiling different components of the virtual texturing system.
    /// </summary>
    public class FrameStat
    {
        // Total accumulated time across all measured frames (in milliseconds)
        private float m_FrameTimeTotal;

        // Number of frames that have been measured
        private int m_FrameCount;

        // Timestamp when the current frame measurement started
        private float m_FrameBeginTime;

        /// <summary>
        /// Time taken by the most recently measured frame (in milliseconds)
        /// </summary>
        public float CurrentTime { get; private set; }

        /// <summary>
        /// Average time per frame across all measured frames (in milliseconds)
        /// </summary>
        public float AverageTime { get; private set; }

        /// <summary>
        /// Maximum time observed for any single frame (in milliseconds)
        /// </summary>
        public float MaxTime { get; private set; }

        /// <summary>
        /// Marks the beginning of a frame measurement.
        /// Should be called before the operation being measured.
        /// </summary>
        public void BeginFrame()
        {
            m_FrameBeginTime = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// Marks the end of a frame measurement and updates statistics.
        /// Should be called after the operation being measured is complete.
        /// </summary>
        public void EndFrame()
        {
            // Skip statistics collection during first 5 seconds to avoid startup anomalies
            if(Time.realtimeSinceStartup < 5)
                return;

            // Calculate time in milliseconds
            var t = (Time.realtimeSinceStartup - m_FrameBeginTime) * 1000;
            m_FrameTimeTotal += t;
            m_FrameCount++;

            CurrentTime = t;
            MaxTime = Mathf.Max(MaxTime, t);
            AverageTime = m_FrameTimeTotal / m_FrameCount;
        }
    }
}