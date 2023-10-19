using JetBrains.Annotations;
using System;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     A wrapper over <see cref="AssetBundle" /> to provide additional data
    /// </summary>
    public class AssetBundleData : IDisposable
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
            Debug.Log($"VV:: Creating AssetBundleData with {gameObject?.name}", gameObject);
            AssetBundle = assetBundle;
            Metrics = metrics;

            GameObject = gameObject;
        }

        public void Dispose()
        {
            AssetBundle.Unload(true);
        }
    }
}
