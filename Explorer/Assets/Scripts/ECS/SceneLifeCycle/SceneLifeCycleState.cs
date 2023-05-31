using Arch.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.SceneLifeCycle
{
    public class SceneLifeCycleState
    {
        public Dictionary<Vector2Int, Ipfs.EntityDefinition> ScenePointers = new();

        public Dictionary<string, Entity> LiveScenes = new();

        public int SceneLoadRadius;

        public Entity PlayerEntity;
    }
}
