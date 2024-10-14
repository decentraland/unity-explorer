using DCL.DebugUtilities.UIBindings;
using DCL.Utilities;

namespace DCL.UserInAppInitializationFlow
{
    public interface ILoadingStatus
    {
        public ReactiveProperty<LoadingStatus.CompletedStage> CurrentCompletedStage { get; }

        public ElementBinding<string> CurrentStageBinding { get; }

        public ElementBinding<string> CurrentAssetsToLoad { get; }
        public ElementBinding<string> CurrentAssetsLoaded { get; }

        void UpdateAssetsLoaded(int assetsLoaded, int assetsToLoad);

        float SetCompletedStage(LoadingStatus.CompletedStage stage);

        void SetCurrentStage(LoadingStatus.CurrentStage stage);
    }
}