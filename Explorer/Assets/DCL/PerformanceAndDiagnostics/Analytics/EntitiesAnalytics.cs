using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class EntitiesAnalytics
    {
        public struct RequestEnvelope : IDisposable
        {
            private readonly DateTime startTime;
            private readonly string eventName;
            private readonly int count;
            private readonly IAnalyticsController analyticsController;

            private int failedEntities;
            private ulong duration;

            public RequestEnvelope(string eventName, int count, IAnalyticsController analyticsController)
            {
                startTime = DateTime.Now;
                this.eventName = eventName;
                this.count = count;
                this.analyticsController = analyticsController;

                failedEntities = 0;
                duration = 0;
            }

            public void OnRequestFinished(int processedEntities)
            {
                failedEntities = count - processedEntities;
                duration = (ulong)(DateTime.Now - startTime).TotalMilliseconds;
            }

            public void Dispose() =>
                analyticsController.Track(eventName, new JObject
                {
                    { "duration", duration },
                    { "count", count },
                    { "failed_count", failedEntities },
                });
        }

        private readonly IAnalyticsController controller;

        public EntitiesAnalytics(IAnalyticsController controller)
        {
            this.controller = controller;
        }

        public RequestEnvelope Track(string eventName, int count) =>
            new (eventName, count, controller);
    }
}
