using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Utilities;
using Segment.Serialization;
using System;
using UnityEngine;
using Utility;

namespace DCL.Analytics.Systems
{
    public class PlayerParcelChangedAnalytics : IDisposable
    {
        private static readonly Vector2Int MIN_INT2 = new (int.MinValue, int.MinValue);

        private readonly IAnalyticsController analytics;
        private readonly IDisposable? subscription;
        private Vector2Int oldParcel;


        public PlayerParcelChangedAnalytics(
            IAnalyticsController analytics,
            PlayerParcelTrackerService parcelTracker)
        {
            this.analytics = analytics;
            ResetOldParcel();
            subscription = parcelTracker.CurrentParcelData.Subscribe(OnParcelChanged);
        }

        private void ResetOldParcel()
        {
            oldParcel = MIN_INT2;
        }

        private void OnParcelChanged(PlayerParcelData newParcelData)
        {
            if (newParcelData.ParcelPosition == oldParcel) return;

            analytics.Track(AnalyticsEvents.World.MOVE_TO_PARCEL, new JsonObject
            {
                { "old_parcel", oldParcel == MIN_INT2 ? "(NaN, NaN)" : oldParcel.ToString() },
                { "new_parcel", newParcelData.ParcelPosition.ToString() },
                { "scene_hash", newParcelData.SceneHash },
                { "is_empty_scene", newParcelData.IsEmptyScene },
            });

            oldParcel = newParcelData.ParcelPosition;
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }
    }
}
