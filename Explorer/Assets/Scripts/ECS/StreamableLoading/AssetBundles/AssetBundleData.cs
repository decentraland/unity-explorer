using Diagnostics.ReportsHandling;
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
        public GameObject GameObject
        {
            get
            {
                if (gameObjectLoaded)
                    return gameObject;

                GameObject[] rootNodes = AssetBundle.LoadAllAssets<GameObject>();

                if (rootNodes.Length > 1)
                    ReportHub.LogError(ReportCategory.ASSET_BUNDLES, $"AssetBundle {AssetBundle.name} contains more than one root game object. Only the first one will be used.");

                return gameObject = rootNodes.Length > 0 ? rootNodes[0] : null;
            }
        }

        [CanBeNull]
        public readonly AssetBundleMetrics? Metrics;

        public AssetBundleData(AssetBundle assetBundle, [CanBeNull] AssetBundleMetrics? metrics /*, GameObject gameObject*/)
        {
            AssetBundle = assetBundle;
            Metrics = metrics;

            //GameObject = gameObject;
        }
    }
}
