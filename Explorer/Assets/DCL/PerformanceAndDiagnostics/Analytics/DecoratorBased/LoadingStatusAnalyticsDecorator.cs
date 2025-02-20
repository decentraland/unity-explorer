using DCL.RealmNavigation;
using DCL.Utilities;
using Segment.Serialization;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class LoadingStatusAnalyticsDecorator : ILoadingStatus
    {
        private readonly ILoadingStatus core;
        private const string STAGE_KEY = "state";
        private int loadingScreenStageId;

        private readonly IAnalyticsController analytics;

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
