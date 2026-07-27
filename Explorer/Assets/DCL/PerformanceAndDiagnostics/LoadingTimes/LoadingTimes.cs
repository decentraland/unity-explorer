using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.RealmNavigation;
using System;
using UnityEngine;

namespace DCL.LoadingTimes
{
    public sealed class LoadingTimes : IDisposable
    {
        private readonly ILoadingStatus loadingStatus;
        private readonly IAnalyticsController analytics;

        public LoadingTimes(ILoadingStatus loadingStatus, IAnalyticsController analytics)
        {
            this.loadingStatus = loadingStatus;
            this.analytics = analytics;

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
                analytics.Track(AnalyticsEvents.Profiling.LOADING_TIMES, LoadingTimeSampler.ToJObject(), true);
#if UNITY_EDITOR
                ReportHub.LogProductionInfo(LoadingTimeSampler.ToJObject().ToString());
#endif
                Application.Quit();
            }
        }
    }
}
