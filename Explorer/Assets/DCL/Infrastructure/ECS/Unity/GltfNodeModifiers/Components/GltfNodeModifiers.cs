using Arch.Core;
using System.Collections.Generic;

namespace ECS.Unity.GltfNodeModifiers.Components
{
    public struct GltfNodeModifiers
    {
        /// <summary>
        ///     Collection of entities created for GLTF nodes that have modifiers applied
        /// </summary>
        public readonly List<Entity> GltfNodeEntities;

        public GltfNodeModifiers(List<Entity> gltfNodeEntities)
        {
            GltfNodeEntities = gltfNodeEntities;
        }
    }
}
