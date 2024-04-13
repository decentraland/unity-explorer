using System;

namespace DCL.Ipfs
{
    [Serializable]
    public class SceneEntityDefinition : EntityDefinitionGeneric<SceneMetadata>
    {
        public SceneEntityDefinition() { }

        public SceneEntityDefinition(string id, SceneMetadata metadata) : base(id, metadata) { }
    }
}
