using System;
using UnityEngine;

namespace DCL.Ipfs
{
    [Serializable]
    public class SceneEntityDefinition : EntityDefinitionGeneric<SceneMetadata>
    {
        private string? logSceneName;
        private SceneFastLookup? SceneLookup;

        public SceneEntityDefinition() { }

        public SceneEntityDefinition(string id, SceneMetadata metadata, AssetBundleManifestVersion? assetBundleManifestVersion = null) : base(id, metadata)
        {
            this.assetBundleManifestVersion = assetBundleManifestVersion;
        }

        public string GetLogSceneName() =>
            logSceneName ??= $"{metadata.scene?.DecodedBase} - {id}";

        /// <summary>
        ///     Whether the scene's models load from the raw GLTF files instead of asset bundles: in local scene development,
        ///     unless local asset bundles are on and the scene's manifest was fetched from the local asset-bundle server.
        /// </summary>
        public bool ShouldLoadRawGltf(bool localSceneDevelopment, bool useLocalAssetBundles) =>
            localSceneDevelopment && (!useLocalAssetBundles || assetBundleManifestVersion is not { assetBundleManifestRequestFailed: false });

        public bool Contains(int x, int y)
        {
            if(SceneLookup.HasValue)
                return SceneLookup.Value.Contains(x, y);

            SceneLookup = new SceneFastLookup(metadata.scene.DecodedParcels);
            return SceneLookup.Value.Contains(x, y);
        }

        public bool Contains(Vector2Int parcel) =>
            Contains(parcel.x, parcel.y);
    }
}
