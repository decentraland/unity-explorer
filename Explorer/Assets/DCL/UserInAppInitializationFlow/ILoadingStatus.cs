using System;
using DCL.DebugUtilities.UIBindings;
using DCL.Utilities;

namespace DCL.UserInAppInitializationFlow
{
    public interface ILoadingStatus
    {
        public ReactiveProperty<LoadingStatus.LoadingStage> CurrentStage { get; }
        
        public ElementBinding<string> CurrentStageBinding { get; }

        public ElementBinding<string> CurrentAssetsStateBinding { get; }

        void UpdateAssetsLoaded(int assetsLoaded, int assetsToLoad);

        float SetCurrentStage(LoadingStatus.LoadingStage stage);
        void ReportAnalytics(Action<LoadingStatus.LoadingStage> onLoadingStageChanged);
    }
}