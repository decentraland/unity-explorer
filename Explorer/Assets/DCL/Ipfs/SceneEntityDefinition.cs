using System;

namespace DCL.Ipfs
{
    [Serializable]
    public class SceneEntityDefinition : EntityDefinitionGeneric<SceneMetadata>
    {
        private string? logSceneName;

        public SceneEntityDefinition() { }

        public SceneEntityDefinition(string id, SceneMetadata metadata, AssetBundleManifestVersion? assetBundleManifestVersion = null) : base(id, metadata)
        {
            this.assetBundleManifestVersion = assetBundleManifestVersion;
        }

        public string GetLogSceneName() =>
            logSceneName ??= $"{metadata.scene?.DecodedBase} - {id}";
    }
}
