using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.AssetsProvision
{
    /// <summary>
    ///     Provides assets for ECS Plugins
    /// </summary>
    public interface IAssetsProvisioner
    {
        /// <summary>
        ///     Provides an original asset such as Scriptable Object, Material, Texture, etc
        /// </summary>
        UniTask<ProvidedAsset<T>> ProvideMainAssetAsync<T>(AssetReferenceT<T> assetReferenceT, CancellationToken ct) where T: Object;

        /// <summary>
        ///     Provides an original prefab from the component reference on its root
        /// </summary>
        UniTask<ProvidedAsset<T>> ProvideMainAssetAsync<T>(ComponentReference<T> componentReference, CancellationToken ct) where T: Object;

        UniTask<ProvidedInstance<T>> ProvideInstanceAsync<T>(ComponentReference<T> componentReference, Vector3 position, Quaternion rotation, Transform? parent = null, CancellationToken ct = default) where T: Object;

        UniTask<ProvidedInstance<T>> ProvideInstanceAsync<T>(ComponentReference<T> componentReference, Transform? parent = null, bool instantiateInWorldSpace = false, CancellationToken ct = default) where T: Object;
    }
}
