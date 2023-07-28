using JetBrains.Annotations;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     A wrapper over <see cref="AssetBundle" /> to provide additional data
    /// </summary>
    public class AssetBundleData
    {
        public readonly AssetBundle AssetBundle;

        /// <summary>
        ///     Root assets - Game Objects
        /// </summary>
        public readonly GameObject GameObject;

        [CanBeNull]
        public readonly AssetBundleMetrics? Metrics;

        public AssetBundleData(AssetBundle assetBundle, [CanBeNull] AssetBundleMetrics? metrics, GameObject gameObject)
        {
            AssetBundle = assetBundle;
            Metrics = metrics;
            GameObject = gameObject;
        }
    }
}
