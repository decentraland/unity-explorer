using System.Collections.Generic;
using UnityEngine.Pool;
using Utility.Multithreading;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Permits every message only once per N frames.
    /// </summary>
    public class FrameDebouncer : TimingBasedDebouncer<long>
    {
        private readonly int frameDebounceThreshold;
        private readonly int cleanUpThreshold;

        private long cleanUpTargetFrame;

        public override ReportHandler AppliedTo => ReportHandler.All;

        public FrameDebouncer(int frameDebounceThreshold)
        {
            this.frameDebounceThreshold = frameDebounceThreshold;
            cleanUpThreshold = frameDebounceThreshold * 100; // Clean up every 100x the debounce threshold
            cleanUpTargetFrame = MultithreadingUtility.FrameCount + cleanUpThreshold;
        }

        protected override bool Debounce(ReportMessageFingerprint fingerprint)
        {
            long currentTiming = MultithreadingUtility.FrameCount;

            CleanUp(currentTiming);

            lock (messages)
            {
                if (!messages.TryGetValue(fingerprint, out long storedTiming))
                {
                    messages[fingerprint] = currentTiming;
                    return false; // First time we see this message
                }

                if (CanPass(storedTiming, currentTiming))
                {
                    messages[fingerprint] = currentTiming;
                    return false; // Enough frames passed, we can log it again
                }

                return true; // Message is debounced
            }
        }

        private bool CanPass(long storedTiming, long currentTiming) =>
            currentTiming - storedTiming > frameDebounceThreshold;

        private bool ShouldCleanUp(long currentTiming)
        {
            if (currentTiming >= cleanUpTargetFrame)
            {
                cleanUpTargetFrame = currentTiming + cleanUpThreshold;
                return true;
            }

            return false;
        }

        private void CleanUp(long timing)
        {
            if (!ShouldCleanUp(timing))
                return;

            lock (messages) { ExecuteCleanUp(timing); }
        }

        private void ExecuteCleanUp(long timing)
        {
            using PooledObject<List<ReportMessageFingerprint>> pooled = ListPool<ReportMessageFingerprint>.Get(out List<ReportMessageFingerprint>? keysToRemove);

            foreach (KeyValuePair<ReportMessageFingerprint, long> kvp in messages)
                if (!CanPass(kvp.Value, timing))
                    keysToRemove.Add(kvp.Key);

            foreach (ReportMessageFingerprint key in keysToRemove) messages.Remove(key);
        }
    }
}
