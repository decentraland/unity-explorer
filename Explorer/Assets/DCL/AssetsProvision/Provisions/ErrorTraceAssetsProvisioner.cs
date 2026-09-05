using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace DCL.AssetsProvision.Provisions
{
    public class ErrorTraceAssetsProvisioner : IAssetsProvisioner
    {
        private readonly IAssetsProvisioner origin;

        public ErrorTraceAssetsProvisioner(IAssetsProvisioner origin)
        {
            this.origin = origin;
        }

        public async UniTask<ProvidedAsset<T>> ProvideMainAssetAsync<T>(AssetReferenceT<T> assetReferenceT, CancellationToken ct) where T: Object
        {
            try { return await origin.ProvideMainAssetAsync(assetReferenceT, ct); }
            catch (Exception e) { throw new Exception($"Cannot provide main asset AssetReferenceT {typeof(T).FullName}", e); }
        }

        public async UniTask<ProvidedAsset<T>> ProvideMainAssetAsync<T>(ComponentReference<T> componentReference, CancellationToken ct) where T: Object
        {
            try { return await origin.ProvideMainAssetAsync(componentReference, ct); }
            catch (Exception e) { throw new Exception($"Cannot provide main asset ComponentReference: {typeof(T).FullName}", e); }
        }

        public async UniTask<ProvidedInstance<T>> ProvideInstanceAsync<T>(ComponentReference<T> componentReference, Vector3 position, Quaternion rotation, Transform parent = null, CancellationToken ct = default) where T: Object
        {
            try { return await origin.ProvideInstanceAsync(componentReference, position, rotation, parent, ct); }
            catch (Exception e) { throw new Exception($"Cannot provide instance ComponentReference: {typeof(T).FullName}", e); }
        }

        public async UniTask<ProvidedInstance<T>> ProvideInstanceAsync<T>(ComponentReference<T> componentReference, Transform parent = null, bool instantiateInWorldSpace = false, CancellationToken ct = default) where T: Object
        {
            try { return await origin.ProvideInstanceAsync(componentReference, parent, instantiateInWorldSpace, ct); }
            catch (Exception e) { throw new Exception($"Cannot provide instance ComponentReference: {typeof(T).FullName}", e); }
        }
    }
}
