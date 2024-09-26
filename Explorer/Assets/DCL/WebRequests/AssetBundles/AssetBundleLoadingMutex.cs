using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.WebRequests
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
        private readonly Func<bool> waitWhile;
        private bool assetsAreBeingLoaded;

        public AssetBundleLoadingMutex()
        {
            waitWhile = () => assetsAreBeingLoaded;
        }

        public async UniTask<LoadingRegion> AcquireAsync(CancellationToken ct)
        {
            await UniTask.WaitWhile(waitWhile, cancellationToken: ct);
            assetsAreBeingLoaded = true;
            return LoadingRegion.Enter(this);
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
    }
}
