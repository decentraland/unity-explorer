using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Utility.Ownership;
using Object = UnityEngine.Object;

namespace DCL.AssetsProvision
{
    public class ContextualAsset<T> : IDisposable where T: Object
    {
        private readonly AssetReferenceT<T> reference;
        private Owned<T>? asset;

        public ContextualAsset(AssetReferenceT<T> reference)
        {
            this.reference = reference;
            asset = null;
        }

        public async UniTask<Weak<T>> AssetAsync(CancellationToken token)
        {
            if (asset == null)
            {
                var handle = reference.LoadAssetAsync();
                await handle.Task!.AsUniTask().AttachExternalCancellation(token);
                if (handle.Status != AsyncOperationStatus.Succeeded) throw new Exception($"Load failed: {reference.RuntimeKey}");
                T value = handle.Result!;
                asset = new Owned<T>(value);
            }

            return asset!.Downgrade();
        }

        public void Release()
        {
            if (asset == null) return;
            reference.ReleaseAsset();
            asset.Dispose(out _);
            asset = null;
        }

        public void Dispose()
        {
            Release();
        }
    }
}
