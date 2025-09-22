using DCL.Diagnostics;
using DCL.RealmNavigation;
using DCL.Utilities;
using Segment.Serialization;
using Sentry;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class LoadingStatusAnalyticsDecorator : ILoadingStatus
    {
        private readonly ILoadingStatus core;
        private const string STAGE_KEY = "state";
        private int loadingScreenStageId;

        private readonly IAnalyticsController analytics;

        private ITransactionTracer mainLoadingTransaction;
        private ISpan currentStageSpan;

        public ReactiveProperty<LoadingStatus.LoadingStage> CurrentStage => core.CurrentStage;
        public ReactiveProperty<string> AssetState => core.AssetState;
        private bool isFirstLoading;

        public LoadingStatusAnalyticsDecorator(ILoadingStatus loadingStatus, IAnalyticsController analytics)
        {
            core = loadingStatus;
            isFirstLoading = true;
            this.analytics = analytics;
        }

        private void OnLoadingStageChanged(LoadingStatus.LoadingStage stage)
        {
            analytics.Track(AnalyticsEvents.General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, $"7.{(byte)stage} - loading screen: {stage.ToString()}" },
            });

            TrackLoadingStageWithSentry(stage);
        }

        private void TrackLoadingStageWithSentry(LoadingStatus.LoadingStage stage)
        {
            if (mainLoadingTransaction == null && stage == LoadingStatus.LoadingStage.Init)
            {
                mainLoadingTransaction = SentrySdk.StartTransaction("loading_process", "loading");
                mainLoadingTransaction.SetTag("loading_type", "initial_loading");
            }

            if (currentStageSpan != null)
            {
                currentStageSpan.Finish();
                currentStageSpan = null;
            }

            if (mainLoadingTransaction != null && stage != LoadingStatus.LoadingStage.Completed)
            {
                currentStageSpan = mainLoadingTransaction.StartChild($"loading_stage_{stage.ToString().ToLower()}", stage.ToString());
                currentStageSpan.SetTag("stage_name", stage.ToString());
            }

            if (stage == LoadingStatus.LoadingStage.Completed && mainLoadingTransaction != null)
            {
                mainLoadingTransaction.SetTag("completed", "true");
                mainLoadingTransaction.Finish();
                mainLoadingTransaction = null;
                currentStageSpan = null;
            }
        }

        public void UpdateAssetsLoaded(int assetsLoaded, int assetsToLoad)
        {
            core.UpdateAssetsLoaded(assetsLoaded, assetsToLoad);
        }

        public float SetCurrentStage(LoadingStatus.LoadingStage stage)
        {
            //After the first loading screen flow, we dont want to report analytics anymore
            if (isFirstLoading)
            {
                OnLoadingStageChanged(stage);
                isFirstLoading = stage != LoadingStatus.LoadingStage.Completed;
            }
            return core.SetCurrentStage(stage);
        }
    }
}
