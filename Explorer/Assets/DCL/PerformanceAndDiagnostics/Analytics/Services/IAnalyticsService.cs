using DCL.PerformanceAndDiagnostics.Analytics.Services;
using Segment.Analytics;
using Segment.Serialization;
using System;
using System.Threading;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    /// <summary>
    /// For the events we use the convention of all lower cases and "_" instead of space
    /// </summary>
    public interface IAnalyticsService
    {
        void Identify(string userId, JsonObject? traits = null);

        /// <summary>
        ///     To track an event you have to call identify first
        /// </summary>
        void Track(string eventName, JsonObject? properties = null);

        void AddPlugin(EventPlugin plugin);

        void Flush();
    }

    public static class AnalyticsServiceExtensions
    {
        public static TimeFlushAnalyticsServiceDecorator WithTimeFlush(this IAnalyticsService service, TimeSpan flushTime, CancellationToken token) =>
            new (service, flushTime, token);

        public static CountFlushAnalyticsServiceDecorator WithCountFlush(this IAnalyticsService service, int flushCount) =>
            new (service, flushCount);
    }
}
