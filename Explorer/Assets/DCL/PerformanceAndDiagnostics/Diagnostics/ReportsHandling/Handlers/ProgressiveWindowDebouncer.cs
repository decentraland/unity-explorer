using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     The more frequent the same message is logged, the more time it takes to log it again.
    /// </summary>
    public class ProgressiveWindowDebouncer : TimingBasedDebouncer<ProgressiveWindowDebouncer.Tracker>
    {
        /// <summary>
        ///     How fast the window grows from the amount of repetitions.
        /// </summary>
        private readonly double backoffFactor;

        private readonly TimeSpan initialWindow;
        private readonly TimeSpan maxWindow;
        private readonly byte allowedRepetitions;
        private readonly TimeSpan cleanUpInterval;

        private readonly Func<DateTime> dateTimeProvider;

        private DateTime lastCleanUpTime;

        public override ReportHandler AppliedTo => ReportHandler.All;

        public ProgressiveWindowDebouncer(TimeSpan initialWindow, TimeSpan maxWindow, TimeSpan cleanUpInterval,
            Func<DateTime>? dateTimeProvider = null,
            double backoffFactor = 1.7,
            byte allowedRepetitions = 3)
        {
            this.initialWindow = initialWindow;
            this.maxWindow = maxWindow;
            this.cleanUpInterval = cleanUpInterval;
            this.dateTimeProvider = dateTimeProvider ?? (() => DateTime.UtcNow);
            this.allowedRepetitions = allowedRepetitions;
            this.backoffFactor = backoffFactor;
            lastCleanUpTime = this.dateTimeProvider();
        }

        protected override bool Debounce<TKey>(Dictionary<TKey, Tracker> dictionary, TKey key)
        {
            DateTime now = dateTimeProvider();

            TryCleanUp(now);

            lock (dictionary)
            {
                Tracker tracker = dictionary.TryGetValue(key, out Tracker existingTracker)
                    ? existingTracker
                    : new Tracker(now, initialWindow);

                bool debounced;

                if (tracker.Count < allowedRepetitions)
                {
                    // allow first N occurrences immediately
                    debounced = false;
                    tracker.LastSeen = now;
                }
                else
                {
                    // compute "pressure" based on how long since first seen
                    TimeSpan targetWindow = CalculateDynamicWindow(now, ref tracker);

                    tracker.Window = targetWindow;

                    if (now - tracker.LastSeen >= tracker.Window)
                    {
                        debounced = false;
                        tracker.LastSeen = now;
                    }
                    else
                        debounced = true;
                }

                tracker.Count++;

                dictionary[key] = tracker; // Reflect the change
                return debounced;
            }
        }

        private TimeSpan CalculateDynamicWindow(DateTime now, ref Tracker tracker)
        {
            // 1) How many “paid” retries have you done?
            int rawRetries = Math.Max(0, tracker.Count - allowedRepetitions);

            if (rawRetries == 0)
                return initialWindow; // nothing to back off yet

            // 2) Compute total idle time since the last send:
            TimeSpan idle = now - tracker.LastSeen;

            // 3) Figure out how many “decay steps” to take:
            //    once you’ve sat idle as long as your current throttle window,
            //    you should lose one retry’s worth of backoff.
            //    Repeat that for each full window you’ve sat idle.
            double currentWindow = initialWindow.TotalSeconds * Math.Pow(backoffFactor, rawRetries);
            currentWindow = Math.Min(currentWindow, maxWindow.TotalSeconds);

            var decaySteps = (short)(idle.TotalSeconds / currentWindow);

            // 4) Net retries = paid retries − decay steps:
            int netRetries = Math.Max(0, rawRetries - decaySteps);
            tracker.Count = Math.Max(allowedRepetitions, (short)(tracker.Count - decaySteps)); // Ensure we don't go below allowed repetitions

            // 5) Recompute the window from netRetries:
            double secs = initialWindow.TotalSeconds * Math.Pow(backoffFactor, netRetries);
            secs = Math.Min(secs, maxWindow.TotalSeconds);

            return TimeSpan.FromSeconds(secs);

            /*int retries = tracker.Count - allowedRepetitions;
            double seconds = initialWindow.TotalSeconds * Math.Pow(backoffFactor, retries);
            return TimeSpan.FromSeconds(Math.Min(maxWindow.TotalSeconds, seconds));*/
        }

        internal bool TryCleanUp(DateTime now)
        {
            if (now - lastCleanUpTime < cleanUpInterval)
                return false;

            lock (messages) { ExecuteCleanUp(now, messages); }

            lock (exceptions) { ExecuteCleanUp(now, exceptions); }

            lastCleanUpTime = now; // Update the last clean-up time
            return true;
        }

        private void ExecuteCleanUp<TKey>(DateTime now, Dictionary<TKey, Tracker> dict)
        {
            using PooledObject<List<TKey>> pooled = ListPool<TKey>.Get(out List<TKey>? keysToRemove);

            foreach (KeyValuePair<TKey, Tracker> kvp in dict)
            {
                Tracker tracker = kvp.Value;
                TKey? key = kvp.Key;
                TimeSpan dynamicWindow = CalculateDynamicWindow(now, ref tracker);

                // If the window has decayed to the initial value or less and exceeded the allowed repetitions window, remove the key
                if (dynamicWindow <= initialWindow && now - tracker.LastSeen > initialWindow * allowedRepetitions)
                    keysToRemove.Add(key);
            }

            foreach (TKey key in keysToRemove) dict.Remove(key);
        }

        public struct Tracker
        {
            public DateTime LastSeen;
            public TimeSpan Window;
            public short Count;

            public Tracker(DateTime lastSeen, TimeSpan window)
            {
                LastSeen = lastSeen;
                Window = window;
                Count = 0;
            }
        }
    }
}
