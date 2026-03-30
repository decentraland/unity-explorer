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

        public bool SupportInitialSceneState()
        {
            if (assetBundleManifestVersion != null)
                return assetBundleManifestVersion.SupportsInitialSceneState();

            return false;
        }

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
