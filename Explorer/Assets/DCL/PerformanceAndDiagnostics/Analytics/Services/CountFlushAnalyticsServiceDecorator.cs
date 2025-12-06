using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;

namespace DCL.PerformanceAndDiagnostics.Analytics.Services
{
    [SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
    public class CountFlushAnalyticsServiceDecorator : IAnalyticsService
    {
        private readonly object monitor = new ();
        private readonly IAnalyticsService origin;
        private readonly int flushCount;
        private int current;

        public CountFlushAnalyticsServiceDecorator(IAnalyticsService origin, int flushCount)
        {
            this.origin = origin;
            this.flushCount = flushCount;
        }

        public void Identify(string? userId, JObject? traits = null)
        {
            lock (monitor)
            {
                origin.Identify(userId, traits);
                IncreaseAndTryFlush();
            }
        }

        public void Track(string eventName, JObject? properties = null)
        {
            lock (monitor)
            {
                origin.Track(eventName, properties);
                IncreaseAndTryFlush();
            }
        }

        public void InstantTrackAndFlush(string eventName, JObject? properties = null)
        {
            lock (monitor)
            {
                origin.InstantTrackAndFlush(eventName, properties);
            }
        }

        public void AddPlugin(IAnalyticsPlugin plugin)
        {
            origin.AddPlugin(plugin);
        }

        public void Flush()
        {
            lock (monitor)
            {
                origin.Flush();
                current = 0;
            }
        }

        private void IncreaseAndTryFlush()
        {
            current++;

            if (current >= flushCount)
            {
                origin.Flush();
                current = 0;
            }
        }
    }
}
