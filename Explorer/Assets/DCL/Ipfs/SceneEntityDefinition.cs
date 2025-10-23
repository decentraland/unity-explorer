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

        public bool SupportInitialSceneState() =>
            assetBundleManifestVersion.SupportsInitialSceneState();
        /*
            //TODO (JUANI): FOr now, we hardcoded it only for GP. We will later check it with manifest
            return metadata.scene.DecodedBase.Equals(new Vector2Int(-9, -9)) ||
                   metadata.scene.DecodedBase.Equals(new Vector2Int(74, -1)) ||
                   metadata.scene.DecodedBase.Equals(new Vector2Int(43, 100));
                   */
    }
}
