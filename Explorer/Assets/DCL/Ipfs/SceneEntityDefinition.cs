using System;
using UnityEngine;

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

        public bool SupportInitialSceneState()
        {
            if (assetBundleManifestVersion != null)
                return assetBundleManifestVersion.SupportsInitialSceneState();

            return false;
        }
    }
}
