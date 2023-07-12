using Ipfs;
using System.Collections.Generic;

namespace ECS.SceneLifeCycle.SceneDefinition
{
    public readonly struct SceneDefinitions
    {
        public readonly List<IpfsTypes.SceneEntityDefinition> Value;

        public SceneDefinitions(List<IpfsTypes.SceneEntityDefinition> value)
        {
            Value = value;
        }
    }
}
