using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine.AddressableAssets;
using Utility.Ownership;
using Object = UnityEngine.Object;

namespace DCL.AssetsProvision
{
    public struct ContextualAsset<T> : IDisposable where T: Object
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly AssetReferenceT<T> reference;
        private readonly CancellationTokenSource cancellationTokenSource;
        private (ProvidedAsset<T> provided, Owned<T> owned)? asset;

        public ContextualAsset(IAssetsProvisioner assetsProvisioner, AssetReferenceT<T> reference) : this()
        {
            this.assetsProvisioner = assetsProvisioner;
            this.reference = reference;
            cancellationTokenSource = new CancellationTokenSource();
            asset = null;
        }

        public async UniTask<Weak<T>> AssetAsync()
        {
            if (asset.HasValue == false)
            {
                ProvidedAsset<T> providedAsset = await assetsProvisioner.ProvideMainAssetAsync(reference, cancellationTokenSource.Token);
                T value = providedAsset.Value;
                Owned<T> ownedAsset = new Owned<T>(value);
                asset = (providedAsset, ownedAsset);
            }

            return asset!.Value.owned!.Downgrade();
        }

        public void Release()
        {
            if (asset.HasValue)
            {
                (ProvidedAsset<T> provided, Owned<T> owned) = asset!.Value;
                provided.Dispose();
                owned!.Dispose(out _);
                asset = null;
            }
        }

        public void Dispose()
        {
            Release();
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
    }
}
