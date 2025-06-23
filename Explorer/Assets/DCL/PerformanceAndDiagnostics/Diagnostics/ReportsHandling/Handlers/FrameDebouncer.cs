using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility.Multithreading;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     Permits every message only once per N frames.
    /// </summary>
    public abstract class FrameDebouncer<TKey> : IReportsDebouncer, IEqualityComparer<TKey>
    {
        private readonly Dictionary<TKey, long> messageFrameCounters;

        private readonly int frameDebounceThreshold;
        private readonly int cleanUpThreshold;

        private long cleanUpTargetFrame;

        protected FrameDebouncer(int frameDebounceThreshold)
        {
            messageFrameCounters = new Dictionary<TKey, long>(1000, this);

            this.frameDebounceThreshold = frameDebounceThreshold;
            cleanUpThreshold = frameDebounceThreshold * 100; // Clean up every 100x the debounce threshold
            cleanUpTargetFrame = MultithreadingUtility.FrameCount + cleanUpThreshold;
        }

        public virtual ReportHandler AppliedTo => ReportHandler.All;

        public bool Debounce(object message, ReportData reportData, LogType log) =>
            Debounce(GetKey(message, reportData, log));

        public bool Debounce(Exception exception, ReportData reportData, LogType log) =>
            Debounce(GetKey(exception, reportData, log));

        private bool Debounce(in TKey key)
        {
            long frameCount = MultithreadingUtility.FrameCount;

            // Clean up old messages
            if (frameCount >= cleanUpTargetFrame)
            {
                CleanUp();
                cleanUpTargetFrame = frameCount + cleanUpThreshold;
            }

            lock (messageFrameCounters)
            {
                if (!messageFrameCounters.TryGetValue(key, out long lastFrame))
                {
                    messageFrameCounters[key] = frameCount;
                    return false; // First time we see this message
                }

                if (frameCount - lastFrame > frameDebounceThreshold)
                {
                    messageFrameCounters[key] = frameCount;
                    return false; // Enough frames passed, we can log it again
                }

                return true; // Message is debounced
            }
        }

        protected abstract TKey GetKey(in object message, ReportData reportData, LogType log);

        protected abstract TKey GetKey(in Exception exception, ReportData reportData, LogType log);

        private void CleanUp()
        {
            long frameCount = MultithreadingUtility.FrameCount;
            using PooledObject<List<TKey>> pooled = ListPool<TKey>.Get(out List<TKey>? keysToRemove);

            lock (messageFrameCounters)
            {
                foreach (KeyValuePair<TKey, long> kvp in messageFrameCounters)
                    if (frameCount - kvp.Value > frameDebounceThreshold)
                        keysToRemove.Add(kvp.Key);

                foreach (TKey key in keysToRemove) { messageFrameCounters.Remove(key); }
            }
        }

        public abstract bool Equals(TKey x, TKey y);

        public abstract int GetHashCode(TKey obj);
    }
}
