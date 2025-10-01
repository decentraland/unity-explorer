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

        public bool SupportInitialSceneState()
        {
            //TODO (JUANI): FOr now, we hardcoded it only for GP. We will later check it with manifest
            return id.Equals("bafkreiggfsyopcam5lrmiiimcqatdrjf4cjtmzuwjf4tgkb5uiq3rvnitq");
        }
    }
}
