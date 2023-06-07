using Arch.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.SceneLifeCycle
{
    public class SceneLifeCycleState
    {
        public readonly Dictionary<Vector2Int, IpfsTypes.SceneEntityDefinition> ScenePointers = new();

        public readonly Dictionary<string, Entity> LiveScenes = new();

        public int SceneLoadRadius;

        public Entity PlayerEntity;
    }
}
