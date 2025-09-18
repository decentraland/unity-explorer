using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using Segment.Serialization;
using System;
using UnityEngine;

namespace DCL.Analytics.Systems
{
    public class PlayerParcelChangedAnalytics : IDisposable
    {
        private static readonly Vector2Int MIN_INT2 = new (int.MinValue, int.MinValue);

        private readonly IAnalyticsController analytics;
        private readonly IDisposable? subscription;
        private readonly IScenesCache scenesCache;

        private Vector2Int oldParcel;


        public PlayerParcelChangedAnalytics(
            IAnalyticsController analytics,
            IScenesCache scenesCache)
        {
            this.analytics = analytics;
            this.scenesCache = scenesCache;
            ResetOldParcel();

            subscription = scenesCache.CurrentParcel.Subscribe(OnParcelChanged);
        }

        private void ResetOldParcel()
        {
            oldParcel = MIN_INT2;
        }

        private void OnParcelChanged(Vector2Int newParcel)
        {
            if (newParcel == oldParcel) return;

            analytics.Track(AnalyticsEvents.World.MOVE_TO_PARCEL, new JsonObject
            {
                { "old_parcel", oldParcel == MIN_INT2 ? "(NaN, NaN)" : oldParcel.ToString() },
                { "new_parcel", newParcel.ToString() },
                { "scene_hash", scenesCache.CurrentScene.Value?.Info.Name},
                { "is_empty_scene", scenesCache.CurrentScene.Value?.IsEmpty},
            });

            oldParcel = newParcel;
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }
    }
}
