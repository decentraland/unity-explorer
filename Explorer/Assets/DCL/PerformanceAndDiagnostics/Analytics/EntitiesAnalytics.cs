using Newtonsoft.Json.Linq;
using System;

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
            private readonly EntitiesAnalyticsDebug.BatchesCounter? batchesCounter;

            private int failedEntities;
            private ulong duration;

            public RequestEnvelope(string eventName, int count, IAnalyticsController analyticsController, EntitiesAnalyticsDebug.BatchesCounter? batchesCounter)
            {
                startTime = DateTime.Now;
                this.eventName = eventName;
                this.count = count;
                this.analyticsController = analyticsController;
                this.batchesCounter = batchesCounter;

                failedEntities = 0;
                duration = 0;
            }

            public void OnRequestFinished(int processedEntities)
            {
                // Add Max, Avg and Min batch size to the debug menu
                batchesCounter?.AddSample(processedEntities);

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
        private readonly EntitiesAnalyticsDebug analyticsDebug;

        public EntitiesAnalytics(IAnalyticsController controller, EntitiesAnalyticsDebug analyticsDebug)
        {
            this.controller = controller;
            this.analyticsDebug = analyticsDebug;

            analyticsDebug.Add(AnalyticsEvents.Endpoints.AVATAR_ATTACHMENT_RETRIEVED)
                          .Add(AnalyticsEvents.Endpoints.SCENE_ENTITIES_RETRIEVED);
        }

        public RequestEnvelope Track(string eventName, int count) =>
            new (eventName, count, controller, analyticsDebug.GetOrDefault(eventName));
    }
}
