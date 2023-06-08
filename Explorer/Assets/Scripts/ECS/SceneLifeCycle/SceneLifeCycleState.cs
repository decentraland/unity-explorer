using Arch.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.SceneLifeCycle
{
    public class SceneLifeCycleState
    {
        /// <summary>
        ///     Scene pointers are cached and never removed
        /// </summary>
        public readonly Dictionary<Vector2Int, ScenePointer> ScenePointers = new ();

        public readonly Dictionary<string, Entity> LiveScenes = new ();

        public int SceneLoadRadius;

        public Entity PlayerEntity;
    }
}
