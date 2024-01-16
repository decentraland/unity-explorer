using Cysharp.Threading.Tasks;

namespace SceneRunner.Scene
{
    /// <summary>
    ///     Via this class the system that runs in the scene world can notify about its [visual] readiness to the upper layer
    /// </summary>
    public class SceneReadinessReport
    {
        public UniTaskCompletionSource CompletionSource { get; }
        public IAsyncReactiveProperty<int> AssetLoadedCount { get; }
        public int TotalAssetsToLoad { get; set; }

        public SceneReadinessReport(UniTaskCompletionSource completionSource,
            IAsyncReactiveProperty<int> assetLoadedCount)
        {
            CompletionSource = completionSource;
            AssetLoadedCount = assetLoadedCount;
        }
    }
}
