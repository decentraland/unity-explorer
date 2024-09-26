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
            // Get the Type object corresponding to TrackEvent
            Type trackEventType = typeof(TrackEvent);

            // Get the constructor info for the internal constructor
            ConstructorInfo constructor = trackEventType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(JsonObject) },
                null);

            if (constructor == null)
                throw new Exception("Constructor not found.");

            // Invoke the constructor with parameters
            object trackEventInstance = constructor.Invoke(new object[] { "eventName", new JsonObject() }).EnsureNotNull();

            // Assuming you want to do something with the created instance
            var track = (TrackEvent)trackEventInstance;
            track.Context = new JsonObject();

            return track;
        }
    }
}
