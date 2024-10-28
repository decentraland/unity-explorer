using Cysharp.Threading.Tasks;
using Segment.Analytics;
using Segment.Serialization;
using System;
using System.Threading;

namespace DCL.PerformanceAndDiagnostics.Analytics.Services
{
    public class TimeFlushAnalyticsServiceDecorator : IAnalyticsService
    {
        private readonly IAnalyticsService origin;
        private readonly TimeSpan flushTime;

        public TimeFlushAnalyticsServiceDecorator(IAnalyticsService origin, TimeSpan flushTime, CancellationToken token)
        {
            this.origin = origin;
            this.flushTime = flushTime;

            FlushLoopAsync(token).Forget();
        }

        public void Identify(string userId, JsonObject? traits = null)
        {
            origin.Identify(userId, traits);
        }

        public void Track(string eventName, JsonObject? properties = null)
        {
            origin.Track(eventName, properties);
        }

        public void AddPlugin(EventPlugin plugin)
        {
            origin.AddPlugin(plugin);
        }

        public void Flush()
        {
            origin.Flush();
        }

        private async UniTaskVoid FlushLoopAsync(CancellationToken token)
        {
            while (token.IsCancellationRequested == false)
            {
                await UniTask.Delay(flushTime, cancellationToken: token);
                Flush();
            }
        }
    }
}
