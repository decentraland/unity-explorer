using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DCL.AssetsProvision
{
    public class AddressablesProvisioner : IAssetsProvisioner
    {
        public async UniTask<ProvidedAsset<T>> ProvideMainAssetAsync<T>(AssetReferenceT<T> assetReferenceT, CancellationToken ct) where T: Object
        {
            // if the main asset was already loaded just return it
            if (assetReferenceT.OperationHandle.IsValid())
                return new ProvidedAsset<T>(assetReferenceT.OperationHandle.Convert<T>());

            AsyncOperationHandle<T> asyncOp = assetReferenceT.LoadAssetAsync();
            await asyncOp.WithCancellation(ct);
            return new ProvidedAsset<T>(asyncOp);
        }

        public async UniTask<ProvidedAsset<T>> ProvideMainAssetAsync<T>(ComponentReference<T> componentReference, CancellationToken ct) where T: Object
        {
            // if the main asset was already loaded just return it
            if (componentReference.OperationHandle.IsValid())
                return new ProvidedAsset<T>(componentReference.OperationHandle.Convert<T>());

            AsyncOperationHandle<T> asyncOp = componentReference.LoadAssetAsync();
            await asyncOp.WithCancellation(ct);
            return new ProvidedAsset<T>(asyncOp);
        }

        public async UniTask<ProvidedInstance<T>> ProvideInstanceAsync<T>(ComponentReference<T> componentReference, Vector3 position, Quaternion rotation, Transform parent = null, CancellationToken ct = default) where T: Object
        {
            AsyncOperationHandle<T> asyncOp = componentReference.InstantiateAsync(position, rotation, parent);
            await asyncOp.WithCancellation(ct);
            return new ProvidedInstance<T>(asyncOp);
        }

        public async UniTask<ProvidedInstance<T>> ProvideInstanceAsync<T>(ComponentReference<T> componentReference, Transform parent = null, bool instantiateInWorldSpace = false, CancellationToken ct = default) where T: Object
        {
            AsyncOperationHandle<T> asyncOp = componentReference.InstantiateAsync(parent, instantiateInWorldSpace);
            await asyncOp.WithCancellation(ct);
            return new ProvidedInstance<T>(asyncOp);
        }
    }
}
