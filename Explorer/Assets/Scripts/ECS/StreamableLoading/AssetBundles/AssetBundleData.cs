using JetBrains.Annotations;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     A wrapper over <see cref="AssetBundle" /> to provide additional data
    /// </summary>
    public class AssetBundleData
    {
        private GameObject gameObject;
        private bool gameObjectLoaded;

        public readonly AssetBundle AssetBundle;

        /// <summary>
        ///     Root assets - Game Objects
        /// </summary>
        public GameObject GameObject { get; }

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
