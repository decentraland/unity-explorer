using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DCL.Landscape.Utils
{
    public class TimeProfiler
    {
        private readonly Stack<Stopwatch> stopwatches = new ();
        private readonly bool enabled;

        public TimeProfiler(bool enabled)
        {
            this.enabled = enabled;
        }

        public void StartMeasure()
        {
            if (!enabled) return;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            stopwatches.Push(stopwatch);
        }

        public void EndMeasure(Action<float> action)
        {
            if (!enabled) return;

            if (stopwatches.Count <= 0)
                throw new Exception("Measure was never started");

            Stopwatch stopwatch = stopwatches.Pop();
            stopwatch.Stop();
            action(stopwatch.ElapsedMilliseconds);
        }
    }
}
