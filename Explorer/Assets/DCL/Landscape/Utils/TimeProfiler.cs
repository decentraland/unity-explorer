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

        public MeasureScope Measure(Action<float> action) =>
            new (this, action);
    }

    public readonly struct MeasureScope : IDisposable
    {
        private readonly TimeProfiler timeProfiler;
        private readonly Action<float> action;

        public MeasureScope(TimeProfiler timeProfiler, Action<float> action)
        {
            this.timeProfiler = timeProfiler;
            this.action = action;
            this.timeProfiler.StartMeasure();
        }

        public void Dispose()
        {
            timeProfiler.EndMeasure(action);
        }
    }
}
