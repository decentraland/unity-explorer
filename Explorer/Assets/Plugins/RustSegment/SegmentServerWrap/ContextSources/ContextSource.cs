using DCL.Utilities.Extensions;
using Segment.Analytics;
using Segment.Serialization;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Plugins.RustSegment.SegmentServerWrap.ContextSources
{
    public class ContextSource : IContextSource
    {
        private readonly List<EventPlugin> plugins = new ();
        private readonly TrackEvent trackEvent = NewTrackEvent();

        public string ContextJson()
        {
            lock (this)
            {
                trackEvent.Context!.Clear();
                foreach (var plugin in plugins) plugin.Track(trackEvent);
                return trackEvent.Context!.ToString() ?? "{}";
            }
        }

        public void Register(EventPlugin plugin)
        {
            lock (this) { plugins.Add(plugin); }
        }

        private static TrackEvent NewTrackEvent()
        {
            Type trackEventType = typeof(TrackEvent);

            ConstructorInfo constructor = trackEventType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(JsonObject) },
                null);

            if (constructor == null)
                throw new Exception("Constructor not found.");

            object trackEventInstance = constructor.Invoke(new object[] { "eventName", new JsonObject() }).EnsureNotNull();

            var track = (TrackEvent)trackEventInstance;
            track.Context = new JsonObject();

            return track;
        }
    }
}
