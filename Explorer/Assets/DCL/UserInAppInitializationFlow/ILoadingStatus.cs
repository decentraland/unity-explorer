using DCL.Utilities;

namespace DCL.UserInAppInitializationFlow
{
    public interface ILoadingStatus
    {
        public ReactiveProperty<LoadingStatus.LoadingStage> CurrentStage { get; }
        public ReactiveProperty<string> AssetState { get; }

        void UpdateAssetsLoaded(int assetsLoaded, int assetsToLoad);

        float SetCurrentStage(LoadingStatus.LoadingStage stage);
    }
}