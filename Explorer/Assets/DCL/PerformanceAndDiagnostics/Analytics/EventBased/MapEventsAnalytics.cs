using DCL.Navmap;
using DCL.PlacesAPIService;
using Segment.Serialization;
using System;
using Utility;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class MapEventsAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly INavmapBus navmapBus;

        public MapEventsAnalytics(IAnalyticsController analytics,
            INavmapBus navmapBus)
        {
            this.analytics = analytics;
            this.navmapBus = navmapBus;

            navmapBus.OnJumpIn += OnJumpIn;
        }

        public void Dispose()
        {
            navmapBus.OnJumpIn -= OnJumpIn;
        }

        private void OnJumpIn(PlacesData.PlaceInfo place)
        {
            if (!VectorUtilities.TryParseVector2Int(place.base_position, out var parcel)) return;

            analytics.Track(AnalyticsEvents.Map.JUMP_IN, new JsonObject
            {
                { "parcel", parcel.ToString() },
            });
        }
    }
}
