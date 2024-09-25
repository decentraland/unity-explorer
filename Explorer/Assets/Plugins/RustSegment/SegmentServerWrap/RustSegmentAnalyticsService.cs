using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics.Analytics;
using Segment.Analytics;
using Segment.Serialization;
using System;

namespace Plugins.RustSegment.SegmentServerWrap
{
    public class RustSegmentAnalyticsService : IAnalyticsService
    {
        private const string EMPTY_JSON = "{}";
        private string cachedUserId = string.Empty;

        public RustSegmentAnalyticsService(string writerKey)
        {
            bool result = NativeMethods.SegmentServerInitialize(writerKey, Callback);

            if (result == false)
                throw new Exception("Rust Segment initialization failed");
        }

        public void Identify(string userId, JsonObject? traits = null)
        {
            cachedUserId = userId;
            ulong operationId = NativeMethods.SegmentServerIdentify(userId, traits?.ToString() ?? EMPTY_JSON, EMPTY_JSON);
        }

        public void Track(string eventName, JsonObject? properties = null)
        {
            ulong operationId = NativeMethods.SegmentServerTrack(cachedUserId, eventName, properties?.ToString() ?? EMPTY_JSON, EMPTY_JSON);
        }

        public void AddPlugin(Plugin plugin)
        {
            //TODO context
            return;
            throw new NotImplementedException();
        }

        public void Flush()
        {
            ulong operationId = NativeMethods.SegmentServerFlush();
        }

        private void Callback(ulong operationId, NativeMethods.Response response)
        {
            ReportHub.Log(ReportCategory.ANALYTICS, $"Segment Operation {operationId} finished with: {response}");
        }
    }
}
