using DCL.Utilities;

namespace DCL.RealmNavigation
{
    /// <summary>
    ///     Read-only view of the loading status, for consumers that only observe the loading
    ///     state and must not drive it (e.g. the loading screen reporting a timeout).
    /// </summary>
    public interface IReadOnlyLoadingStatus
    {
        public ReactiveProperty<LoadingStatus.LoadingStage> CurrentStage { get; }
        public ReactiveProperty<string> AssetState { get; }

        bool IsLoadingScreenOn();
    }

    public interface ILoadingStatus : IReadOnlyLoadingStatus
    {
        void UpdateAssetsLoaded(int assetsLoaded, int assetsToLoad);

        float SetCurrentStage(LoadingStatus.LoadingStage stage);
    }
}
