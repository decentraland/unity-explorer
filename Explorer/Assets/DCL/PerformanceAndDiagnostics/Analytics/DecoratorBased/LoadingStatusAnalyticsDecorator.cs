using System;
using DCL.UserInAppInitializationFlow;
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
        private bool firstLoginDone;

        public LoadingStatusAnalyticsDecorator(ILoadingStatus loadingStatus, IAnalyticsController analytics)
        {
            core = loadingStatus;
            this.analytics = analytics;
        }


        private void OnLoadingStageChanged(LoadingStatus.LoadingStage stage)
        {
            analytics.Track(AnalyticsEvents.General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, $"7.{loadingScreenStageId++} - loading screen: {stage}" },
            });
        }

        public void UpdateAssetsLoaded(int assetsLoaded, int assetsToLoad)
        {
            core.UpdateAssetsLoaded(assetsLoaded, assetsToLoad);
        }

        public float SetCurrentStage(LoadingStatus.LoadingStage stage)
        {            
            //After the first loading screen flow, we dont want to report analytics anymore
            if (!firstLoginDone)
            {
                OnLoadingStageChanged(stage);
                firstLoginDone = stage == LoadingStatus.LoadingStage.Completed;
            }
            return core.SetCurrentStage(stage);
        }

     
    }
}