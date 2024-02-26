using Cysharp.Threading.Tasks;
using DCL.AssetsProvision.Provisions;
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

        public static ErrorTraceAssetsProvisioner WithErrorTrace(this IAssetsProvisioner origin) =>
            new (origin);

        public static ValidatesAssetsProvisioner WithValidates(this IAssetsProvisioner origin) =>
            new (origin);
    }
}
