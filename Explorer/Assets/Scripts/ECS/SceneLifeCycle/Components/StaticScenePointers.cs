using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.SceneLifeCycle.Components
{
    /// <summary>
    ///     For debug purposes - load scenes from the list, ignore all other logic
    /// </summary>
    public struct StaticScenePointers
    {
        public readonly IReadOnlyList<Vector2Int> Value;

        public AssetPromise<SceneDefinitions, GetSceneDefinitionList>? Promise;

        public StaticScenePointers(IReadOnlyList<Vector2Int> value)
        {
            Value = value;
            Promise = null;
        }
    }
}
