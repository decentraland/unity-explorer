using DCL.Diagnostics;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DCL.AssetsProvision
{
    /// <summary>
    ///     Denotes the root asset (such as Scriptable Object) that was loaded from Addressables
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct ProvidedAsset<T> where T: Object
    {
        public readonly T Value;

        private AsyncOperationHandle<T> handle;

        public ProvidedAsset(T instantValue) : this()
        {
            Value = instantValue;
        }

        public ProvidedAsset(AsyncOperationHandle<T> handle)
        {
            this.handle = handle;
            Value = handle.Result;
        }

        /// <summary>
        ///     <para>Release the asset itself</para>
        ///     <see cref="AssetReferenceT{TObject}" />
        /// </summary>
        public void Dispose()
        {
            if (!handle.IsValid())
            {
                ReportHub.LogWarning(ReportCategory.ASSETS_PROVISION, $"Cannot release a null or unloaded asset of type {typeof(T)}.");
                return;
            }

            Addressables.Release(handle);
            handle = default(AsyncOperationHandle<T>);
        }
    }
}
