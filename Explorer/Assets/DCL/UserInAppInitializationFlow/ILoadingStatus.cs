using DCL.DebugUtilities.UIBindings;
using DCL.Utilities;

namespace DCL.UserInAppInitializationFlow
{
    public interface ILoadingStatus
    {
        public ReactiveProperty<LoadingStatus.Stage> CurrentCompletedStage { get; }

        public ElementBinding<string> CurrentCompletedStageBinding { get; }

        public ElementBinding<string> CurrentAssetsToLoad { get; }
        public ElementBinding<string> CurrentAssetsLoaded { get; }

        void UpdateAssetsLoaded(int assetsLoaded, int assetsToLoad);

        float SetCompletedStage(LoadingStatus.Stage stage);
    }
}