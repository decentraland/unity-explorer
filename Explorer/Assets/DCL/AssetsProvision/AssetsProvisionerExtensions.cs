using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.AssetsProvision
{
    public static class AssetsProvisionerExtensions
    {
        public static UniTask<T> ProvideMainAssetValueAsync<T>(this IAssetsProvisioner assetsProvisioner, AssetReferenceT<T> assetReferenceT, CancellationToken ct) where T: Object
        {
            return assetsProvisioner.ProvideMainAssetAsync(assetReferenceT, ct).ContinueWith(x => x.Value);
        }
    }
}
