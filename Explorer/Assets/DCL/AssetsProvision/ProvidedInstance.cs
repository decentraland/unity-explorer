using DCL.Diagnostics;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace DCL.AssetsProvision
{
    /// <summary>
    ///     Denotes a prefab instance with a root component that was loaded from Addressables
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct ProvidedInstance<T> : IDisposable where T: Object
    {
        public readonly T Value;

        private AsyncOperationHandle<T> handle;

        public ProvidedInstance(AsyncOperationHandle<T> handle)
        {
            this.handle = handle;
            Value = handle.Result;
        }

        /// <summary>
        ///     <para>Releases Instance of the prefab</para>
        ///     <see cref="ComponentReference{TComponent}" />
        /// </summary>
        public void Dispose()
        {
            if (!handle.IsValid())
            {
                ReportHub.LogWarning(ReportCategory.ASSETS_PROVISION, $"Cannot release a null or unloaded asset of type {typeof(T)}.");
                return;
            }

            // Release the instance
            var component = handle.Result as Component;

            if (component != null)
                Addressables.ReleaseInstance(component.gameObject);

            // Release the handle
            Addressables.Release(handle);

            handle = default(AsyncOperationHandle<T>);
        }
    }
}
