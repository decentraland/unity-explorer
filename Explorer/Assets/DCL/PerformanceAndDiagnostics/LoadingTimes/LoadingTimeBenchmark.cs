using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.RealmNavigation;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.LoadingTimes
{
    /// <summary>
    ///     CI-only benchmark of the startup loading stages: reports their durations to analytics and quits the client.
    /// </summary>
    public sealed class LoadingTimeBenchmark : IDisposable
    {
        private const string STAGE_PREFIX = "loading_stage_";
        private const string START_LABEL = "start_time_s";
        private const string STOP_LABEL = "stop_time_s";
        private const string DURATION_LABEL = "duration_s";
        private const string SCENE_HASH_LABEL = "scene_hash";
        private const string PLATFORM_LABEL = "platform";

#if UNITY_STANDALONE_OSX
        private const string PLATFORM_VALUE = "mac";
#else
        private const string PLATFORM_VALUE = "pc";
#endif

        // The loading begins with the process, where realtimeSinceStartup is 0.
        private const float APP_START_TIME = 0f;

        // The analytics service dispatches on a background pump, quitting right away kills the request.
        private static readonly TimeSpan ANALYTICS_DELIVERY_GRACE = TimeSpan.FromSeconds(5);

        private readonly List<StageMeasure> measures = new ((int)LoadingStatus.LoadingStage.Completed + 1);
        private readonly CancellationTokenSource cts = new ();

        private readonly ILoadingStatus loadingStatus;
        private readonly IAnalyticsController analytics;
        private readonly IScenesCache scenesCache;

        private bool reported;

        public LoadingTimeBenchmark(ILoadingStatus loadingStatus, IAnalyticsController analytics, IScenesCache scenesCache)
        {
            this.loadingStatus = loadingStatus;
            this.analytics = analytics;
            this.scenesCache = scenesCache;

            // Init is set before this subscription exists, so it can only be measured from the app start.
            measures.Add(new StageMeasure
            {
                Stage = LoadingStatus.LoadingStage.Init,
                StartTime = APP_START_TIME,
                StopTime = APP_START_TIME,
            });

            loadingStatus.CurrentStageMut.OnUpdate += OnStageUpdated;
        }

        public void Dispose()
        {
            loadingStatus.CurrentStageMut.OnUpdate -= OnStageUpdated;
            cts.SafeCancelAndDispose();
        }

        private void OnStageUpdated(LoadingStatus.LoadingStage stage)
        {
            if (reported) return;

            Sample(stage);

            if (stage != LoadingStatus.LoadingStage.Completed) return;

            reported = true;
            ReportAndQuitAsync(scenesCache.CurrentScene.Value?.Info.Name).Forget();
        }

        private void Sample(LoadingStatus.LoadingStage stage)
        {
            float time = UnityEngine.Time.realtimeSinceStartup;

            StageMeasure previous = measures[^1];
            previous.StopTime = time;
            measures[^1] = previous;

            measures.Add(new StageMeasure
            {
                Stage = stage,
                StartTime = time,
                StopTime = time,
            });
        }

        private JObject BuildPayload(string? sceneHash)
        {
            float stopTime = measures[^1].StopTime;

            var payload = new JObject
            {
                { SCENE_HASH_LABEL, sceneHash },
                { PLATFORM_LABEL, PLATFORM_VALUE },
                { START_LABEL, APP_START_TIME },
                { STOP_LABEL, stopTime },
                { DURATION_LABEL, stopTime - APP_START_TIME },
            };

            foreach (StageMeasure measure in measures)
                payload[$"{STAGE_PREFIX}{measure.Stage.ToString().ToLower()}"] = new JObject
                {
                    { START_LABEL, measure.StartTime },
                    { STOP_LABEL, measure.StopTime },
                    { DURATION_LABEL, measure.StopTime - measure.StartTime },
                };

            return payload;
        }

        private async UniTaskVoid ReportAndQuitAsync(string? sceneHash)
        {
            try
            {
                JObject payload = BuildPayload(sceneHash);

                analytics.Track(AnalyticsEvents.Profiling.LOADING_TIMES, payload, true);
#if UNITY_EDITOR
                ReportHub.LogProductionInfo(payload.ToString());
#endif

                await UniTask.Delay(ANALYTICS_DELIVERY_GRACE, cancellationToken: cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Disposal means the shutdown is already under way, quitting again would be redundant.
                return;
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.ANALYTICS);
            }

            Application.Quit();
        }

        private struct StageMeasure
        {
            public LoadingStatus.LoadingStage Stage;
            public float StartTime;
            public float StopTime;
        }
    }
}
