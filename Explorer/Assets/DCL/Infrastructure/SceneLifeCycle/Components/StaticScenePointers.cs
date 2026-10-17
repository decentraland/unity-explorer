using DCL.Ipfs;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using System.Collections.Generic;
using Unity.Mathematics;

namespace ECS.SceneLifeCycle.Components
{
    /// <summary>
    ///     For debug purposes - load scenes from the list, ignore all other logic
    /// </summary>
    public struct StaticScenePointers
    {
        public readonly IReadOnlyList<int2> Value;

        public AssetPromise<SceneDefinitions, GetSceneDefinitionList>? Promise;

        public StaticScenePointers(IReadOnlyList<int2> value)
        {
            Value = value;
            Promise = null;
        }
    }
}
