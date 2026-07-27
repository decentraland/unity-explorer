using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.RealmNavigation;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

namespace DCL.LoadingTimes
{
    public sealed class LoadingTimes : IDisposable
    {
        private readonly ILoadingStatus loadingStatus;
        private readonly IAnalyticsController analytics;
        private readonly IScenesCache scenesCache;

        public LoadingTimes(ILoadingStatus loadingStatus, IAnalyticsController analytics, IScenesCache scenesCache)
        {
            this.loadingStatus = loadingStatus;
            this.analytics = analytics;
            this.scenesCache = scenesCache;

            loadingStatus.CurrentStageMut.OnUpdate += OnStageUpdated;
        }

        public void Dispose()
        {
            loadingStatus.CurrentStageMut.OnUpdate -= OnStageUpdated;
        }

        private void OnStageUpdated(LoadingStatus.LoadingStage stage)
        {
            LoadingTimeSampler.Sample(stage);

            if (stage == LoadingStatus.LoadingStage.Completed)
            {
                JObject payload = LoadingTimeSampler.ToJObject(scenesCache.CurrentScene.Value?.Info.Name);

                analytics.Track(AnalyticsEvents.Profiling.LOADING_TIMES, payload, true);
#if UNITY_EDITOR
                ReportHub.LogProductionInfo(payload.ToString());
#endif
                Application.Quit();
            }
        }
    }
}
