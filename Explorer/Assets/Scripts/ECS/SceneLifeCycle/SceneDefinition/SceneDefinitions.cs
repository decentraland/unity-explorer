using DCL.Ipfs;
using Ipfs;
using System.Collections.Generic;

namespace ECS.SceneLifeCycle.SceneDefinition
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
