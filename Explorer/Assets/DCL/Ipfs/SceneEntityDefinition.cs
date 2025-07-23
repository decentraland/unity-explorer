using SceneRunner.Scene;
using System;

namespace DCL.Ipfs
{
    [Serializable]
    public class SceneEntityDefinition : EntityDefinitionGeneric<SceneMetadata>
    {
        private string? logSceneName;

        public SceneEntityDefinition() { }

        public SceneEntityDefinition(string id, SceneMetadata metadata) : base(id, metadata) { }

        public string GetLogSceneName() =>
            logSceneName ??= $"{metadata.scene?.DecodedBase} - {id}";

        //TODO (JUANI): This could be here, since we could be getting it from the asset-bundle-registry
        public SceneAssetBundleManifest AssetBundleManifest;
    }
}
