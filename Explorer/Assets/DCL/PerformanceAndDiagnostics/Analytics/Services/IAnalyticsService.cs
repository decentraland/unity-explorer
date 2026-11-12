using DCL.PerformanceAndDiagnostics.Analytics.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    /// <summary>
    /// For the events we use the convention of all lower cases and "_" instead of space
    /// </summary>
    public interface IAnalyticsService
    {
        /// <summary>
        ///     Accepts every call and sends nothing, for sessions whose activity must not reach the analytics backend.
        /// </summary>
        public static IAnalyticsService Null => NullAnalyticsService.INSTANCE;

        void Identify(string? userId, JObject? traits = null);

        /// <summary>
        ///     To track an event you have to call identify first
        /// </summary>
        void Track(string eventName, JObject? properties = null);

        void InstantTrackAndFlush(string eventName, JObject? properties = null);

        void AddPlugin(IAnalyticsPlugin plugin);

        void Flush();

        private sealed class NullAnalyticsService : IAnalyticsService
        {
            internal static readonly NullAnalyticsService INSTANCE = new ();

            private NullAnalyticsService() { }

            public void Identify(string? _, JObject? __ = null) { }

            public void Track(string _, JObject? __ = null) { }

            public void InstantTrackAndFlush(string _, JObject? __ = null) { }

            public void AddPlugin(IAnalyticsPlugin _) { }

            public void Flush() { }
        }
    }

    public interface IAnalyticsPlugin
    {
        void Track(JObject mutableContext);
    }

    public static class AnalyticsServiceExtensions
    {
        public static TimeFlushAnalyticsServiceDecorator WithTimeFlush(this IAnalyticsService service, TimeSpan flushTime, CancellationToken token) =>
            new (service, flushTime, token);

        public static CountFlushAnalyticsServiceDecorator WithCountFlush(this IAnalyticsService service, int flushCount) =>
            new (service, flushCount);
    }

    public abstract class RawEvent
    {
        public virtual string Type { get; set; }
        public virtual string AnonymousId { get; set; }
        public virtual string MessageId { get; set; }
        public virtual string UserId { get; set; }
        public virtual string Timestamp { get; set; }

        [JsonIgnore]
        public Func<RawEvent, RawEvent> Enrichment { get; set; }

        // JSON types
        public JObject Context { get; set; }
        public JObject Integrations { get; set; }

        public JArray Metrics { get; set; }

        internal void ApplyRawEventData(RawEvent rawEvent)
        {
            AnonymousId = rawEvent.AnonymousId;
            MessageId = rawEvent.MessageId;
            UserId = rawEvent.UserId;
            Timestamp = rawEvent.Timestamp;
            Context = rawEvent.Context;
            Integrations = rawEvent.Integrations;
        }
    }

    public sealed class TrackEvent : RawEvent
    {
        public override string Type => "track";

        public string Event { get; set; }

        public JObject Properties { get; set; }

        internal TrackEvent(string trackEvent, JObject properties)
        {
            Event = trackEvent;
            Properties = properties;
        }

        internal TrackEvent(TrackEvent existing) : this(existing.Event, existing.Properties) =>
            ApplyRawEventData(existing);
    }
}
