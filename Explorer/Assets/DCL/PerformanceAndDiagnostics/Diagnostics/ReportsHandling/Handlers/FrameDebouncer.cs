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

        protected override bool Debounce<TKey>(Dictionary<TKey, long> dictionary, TKey key)
        {
            long currentTiming = MultithreadingUtility.FrameCount;

            CleanUp(currentTiming);

            lock (dictionary)
            {
                if (!dictionary.TryGetValue(key, out long storedTiming))
                {
                    dictionary[key] = currentTiming;
                    return false; // First time we see this message
                }

                if (CanPass(storedTiming, currentTiming))
                {
                    dictionary[key] = currentTiming;
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

            lock (messages) { ExecuteCleanUp(timing, messages); }

            lock (exceptions) { ExecuteCleanUp(timing, exceptions); }
        }

        private void ExecuteCleanUp<TKey>(long timing, Dictionary<TKey, long> dict)
        {
            using PooledObject<List<TKey>> pooled = ListPool<TKey>.Get(out List<TKey>? keysToRemove);

            foreach (KeyValuePair<TKey, long> kvp in dict)
                if (!CanPass(kvp.Value, timing))
                    keysToRemove.Add(kvp.Key);

            foreach (TKey key in keysToRemove) dict.Remove(key);
        }
    }
}
