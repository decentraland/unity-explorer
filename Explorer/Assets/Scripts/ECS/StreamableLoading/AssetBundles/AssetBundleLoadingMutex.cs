using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     Guarantees that only one asset bundle is being loaded at a time
    ///     <para>
    ///         It is needed to prevent `Loading.LockPersistentManager` hiccup
    ///         as it is being synchronized by the main thread
    ///     </para>
    /// </summary>
    public class AssetBundleLoadingMutex
    {
        private bool assetsAreBeingLoaded;

        private readonly Func<bool> waitWhile;

        public AssetBundleLoadingMutex()
        {
            waitWhile = () => assetsAreBeingLoaded;
        }

        public struct LoadingRegion : IDisposable
        {
            private readonly AssetBundleLoadingMutex mutex;

            private LoadingRegion(AssetBundleLoadingMutex mutex)
            {
                this.mutex = mutex;
            }

            internal static LoadingRegion Enter(AssetBundleLoadingMutex mutex) =>
                new (mutex);

            public void Dispose()
            {
                mutex.assetsAreBeingLoaded = false;
            }
        }

        public async UniTask<LoadingRegion> Acquire(CancellationToken ct)
        {
            await UniTask.WaitWhile(waitWhile, cancellationToken: ct);
            assetsAreBeingLoaded = true;
            return LoadingRegion.Enter(this);
        }
    }
}
