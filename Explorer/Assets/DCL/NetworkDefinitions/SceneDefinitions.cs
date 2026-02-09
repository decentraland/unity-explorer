using System.Collections.Generic;

namespace DCL.Ipfs
{
    public readonly struct SceneDefinitions
    {
        public readonly List<SceneEntityDefinition> Value;

        public SceneDefinitions(List<SceneEntityDefinition> value)
        {
            Value = value;
        }
    }
}
