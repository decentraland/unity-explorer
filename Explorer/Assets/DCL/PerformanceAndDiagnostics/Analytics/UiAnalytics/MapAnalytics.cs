using DCL.Navmap;
using Segment.Serialization;
using System;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class MapAnalytics : IDisposable
    {
        private readonly AnalyticsController analytics;
        private readonly NavmapController navmapController;

        public MapAnalytics(AnalyticsController analytics, NavmapController navmapController)
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
