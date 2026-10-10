using Cysharp.Threading.Tasks;
using DCL.AssetsProvision.Provisions;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace DCL.AssetsProvision
{
    public static class AssetsProvisionerExtensions
    {
        public static UniTask<T> ProvideMainAssetValueAsync<T>(this IAssetsProvisioner assetsProvisioner, AssetReferenceT<T> assetReferenceT, CancellationToken ct) where T: Object
        {
            return assetsProvisioner.ProvideMainAssetAsync(assetReferenceT, ct).ContinueWith(x => x.Value);
        }

        public static async UniTask<ProvidedAsset<T>> ProvideMainAssetAsync<T>(this IAssetsProvisioner assetsProvisioner, AssetReferenceT<T> assetReferenceT, CancellationToken ct, string error) where T: Object
        {
            try { return await assetsProvisioner.ProvideMainAssetAsync(assetReferenceT, ct); }
            catch (Exception e) { throw new Exception($"Cannot provide main asset: {error}", e); }
        }

        public static async UniTask<ProvidedInstance<T>> ProvideInstanceAsync<T>(
            this IAssetsProvisioner assetsProvisioner,
            ComponentReference<T> componentReference,
            Vector3 position,
            Quaternion rotation,
            Transform? parent = null,
            CancellationToken ct = default,
            string error = ""
        ) where T: Object
        {
            try { return await assetsProvisioner.ProvideInstanceAsync(componentReference, position, rotation, parent, ct); }
            catch (Exception e) { throw new Exception($"Cannot provide instance: {error}", e); }
        }

        public static ErrorTraceAssetsProvisioner WithErrorTrace(this IAssetsProvisioner origin) =>
            new (origin);
    }
}
