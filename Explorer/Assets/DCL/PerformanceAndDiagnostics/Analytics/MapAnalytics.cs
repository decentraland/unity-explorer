using DCL.Navmap;
using Segment.Serialization;
using System;
using System.Collections.Generic;
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
            analytics.Track("map_jump_in", new Dictionary<string, JsonElement>
            {
                { "parcel", parcel.ToString() },
            });
        }
    }
}
