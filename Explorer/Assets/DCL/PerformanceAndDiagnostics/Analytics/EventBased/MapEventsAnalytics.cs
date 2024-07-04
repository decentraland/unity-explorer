using DCL.Navmap;
using Segment.Serialization;
using System;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class MapEventsAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly NavmapController navmapController;

        public MapEventsAnalytics(IAnalyticsController analytics, NavmapController navmapController)
        {
            this.analytics = analytics;
            this.navmapController = navmapController;

            this.navmapController.FloatingPanelController.OnJumpIn += OnJumpIn;
        }

        public void Dispose()
        {
            navmapController.FloatingPanelController.OnJumpIn -= OnJumpIn;
        }

        private void OnJumpIn(Vector2Int parcel)
        {
            analytics.Track(AnalyticsEvents.Map.JUMP_IN, new JsonObject
            {
                { "parcel", parcel.ToString() },
            });
        }
    }
}
