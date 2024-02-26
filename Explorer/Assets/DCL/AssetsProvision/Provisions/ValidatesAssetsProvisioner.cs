using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace DCL.AssetsProvision.Provisions
{
    public class ValidatesAssetsProvisioner : IAssetsProvisioner
    {
        private readonly IAssetsProvisioner origin;

        public ValidatesAssetsProvisioner(IAssetsProvisioner origin)
        {
            this.origin = origin;
        }

        public UniTask<ProvidedAsset<T>> ProvideMainAssetAsync<T>(AssetReferenceT<T> assetReferenceT, CancellationToken ct) where T: Object
        {
            if (assetReferenceT.IsValid() == false)
                throw new Exception($"AssetReferenceT {assetReferenceT.AssetGUID} is not valid");

            return origin.ProvideMainAssetAsync(assetReferenceT, ct);
        }

        public UniTask<ProvidedAsset<T>> ProvideMainAssetAsync<T>(ComponentReference<T> componentReference, CancellationToken ct) where T: Object
        {
            if (componentReference.IsValid() == false)
                throw new Exception($"ComponentReference {componentReference.AssetGUID} is not valid");

            return origin.ProvideMainAssetAsync(componentReference, ct);
        }

        public UniTask<ProvidedInstance<T>> ProvideInstanceAsync<T>(ComponentReference<T> componentReference, Vector3 position, Quaternion rotation, Transform? parent = null, CancellationToken ct = default) where T: Object
        {
            if (componentReference.IsValid() == false)
                throw new Exception($"ComponentReference {componentReference.AssetGUID} is not valid");

            return origin.ProvideInstanceAsync(componentReference, position, rotation, parent, ct);
        }

        public UniTask<ProvidedInstance<T>> ProvideInstanceAsync<T>(ComponentReference<T> componentReference, Transform? parent = null, bool instantiateInWorldSpace = false, CancellationToken ct = default) where T: Object
        {
            if (componentReference.IsValid() == false)
                throw new Exception($"ComponentReference {componentReference.AssetGUID} is not valid");

            return origin.ProvideInstanceAsync(componentReference, parent, instantiateInWorldSpace, ct);
        }
    }
}
