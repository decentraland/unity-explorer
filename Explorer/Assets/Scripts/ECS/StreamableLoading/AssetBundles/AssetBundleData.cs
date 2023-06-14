using JetBrains.Annotations;
using System.Collections.Generic;
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
        public readonly IReadOnlyList<GameObject> GameObjectNodes;

        [CanBeNull]
        public readonly AssetBundleMetrics? Metrics;

        public AssetBundleData(AssetBundle assetBundle, [CanBeNull] AssetBundleMetrics? metrics, IReadOnlyList<GameObject> gameObjectNodes)
        {
            AssetBundle = assetBundle;
            Metrics = metrics;
            GameObjectNodes = gameObjectNodes;
        }
    }
}
