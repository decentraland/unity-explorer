using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using Temp.Helper.WebClient;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace DCL.AssetsProvision
{
    public class AddressablesProvisioner : IAssetsProvisioner
    {
        private static void LogAssetRequest(string op, string assetKey, string typeName) =>
            WebGLDebugLog.Log(op, assetKey, typeName);

        public async UniTask<ProvidedAsset<T>> ProvideMainAssetAsync<T>(AssetReferenceT<T> assetReferenceT, CancellationToken ct) where T: Object
        {
            LogAssetRequest("ProvideMainAssetAsync", assetReferenceT?.RuntimeKey?.ToString() ?? "null", typeof(T).Name);
            // if the main asset was already loaded just return it
            if (assetReferenceT.OperationHandle.IsValid())
                return new ProvidedAsset<T>(assetReferenceT.OperationHandle.Convert<T>());

            AsyncOperationHandle<T> asyncOp = assetReferenceT.LoadAssetAsync();
            await asyncOp.WithCancellation(ct);
            EnsureLoadSucceeded(asyncOp, assetReferenceT?.RuntimeKey?.ToString(), typeof(T).Name);
            return new ProvidedAsset<T>(asyncOp);
        }

        public async UniTask<ProvidedAsset<T>> ProvideMainAssetAsync<T>(ComponentReference<T> componentReference, CancellationToken ct) where T: Object
        {
            LogAssetRequest("ProvideMainAssetAsync", componentReference?.RuntimeKey?.ToString() ?? "null", typeof(T).Name);
            // if the main asset was already loaded just return it
            if (componentReference.OperationHandle.IsValid())
                return new ProvidedAsset<T>(componentReference.OperationHandle.Convert<T>());

            AsyncOperationHandle<T> asyncOp = componentReference.LoadAssetAsync();
            await asyncOp.WithCancellation(ct);
            EnsureLoadSucceeded(asyncOp, componentReference?.RuntimeKey?.ToString(), typeof(T).Name);
            return new ProvidedAsset<T>(asyncOp);
        }

        private static void EnsureLoadSucceeded<T>(AsyncOperationHandle<T> handle, string? assetKey, string typeName) where T : Object
        {
            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
                return;
            string msg = $"Addressables load failed: key={assetKey} type={typeName} status={handle.Status}";
            if (handle.OperationException != null)
                throw new InvalidOperationException(msg, handle.OperationException);
            throw new InvalidOperationException(msg + " (Result is null or status not Succeeded).");
        }

        public async UniTask<ProvidedInstance<T>> ProvideInstanceAsync<T>(ComponentReference<T> componentReference, Vector3 position, Quaternion rotation, Transform parent = null, CancellationToken ct = default) where T: Object
        {
            LogAssetRequest("ProvideInstanceAsync", componentReference?.RuntimeKey?.ToString() ?? "null", typeof(T).Name);
            AsyncOperationHandle<T> asyncOp = componentReference.InstantiateAsync(position, rotation, parent);
            await asyncOp.WithCancellation(ct);
            return new ProvidedInstance<T>(asyncOp);
        }

        public async UniTask<ProvidedInstance<T>> ProvideInstanceAsync<T>(ComponentReference<T> componentReference, Transform parent = null, bool instantiateInWorldSpace = false, CancellationToken ct = default) where T: Object
        {
            LogAssetRequest("ProvideInstanceAsync", componentReference?.RuntimeKey?.ToString() ?? "null", typeof(T).Name);
            AsyncOperationHandle<T> asyncOp = componentReference.InstantiateAsync(parent, instantiateInWorldSpace);
            await asyncOp.WithCancellation(ct);
            return new ProvidedInstance<T>(asyncOp);
        }
    }
}
