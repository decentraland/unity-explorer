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

        [CanBeNull]
        public readonly AssetBundleMetrics? Metrics;
        private GameObject gameObject;
        private bool gameObjectLoaded;

        /// <summary>
        ///     Root assets - Game Objects
        /// </summary>
        public GameObject GameObject { get; }

        public AssetBundleData(AssetBundle assetBundle, [CanBeNull] AssetBundleMetrics? metrics, GameObject gameObject)
        {
            AssetBundle = assetBundle;
            Metrics = metrics;

            GameObject = gameObject;
        }
    }
}
