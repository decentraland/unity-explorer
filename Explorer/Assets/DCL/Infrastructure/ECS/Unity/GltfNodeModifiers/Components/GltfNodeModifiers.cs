using Arch.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.GltfNodeModifiers.Components
{
    public struct GltfNodeModifiers
    {
        /// <summary>
        /// Tracks GltfNode entities and their corresponding node paths
        /// </summary>
        public readonly Dictionary<Entity, string> GltfNodeEntities;

        /// <summary>
        /// Stores original materials before modification for restoration during cleanup
        /// </summary>
        public readonly Dictionary<Renderer, Material> OriginalMaterials;

        public GltfNodeModifiers(Dictionary<Entity, string> gltfNodeEntities, Dictionary<Renderer, Material> originalMaterials)
        {
            GltfNodeEntities = gltfNodeEntities;
            OriginalMaterials = originalMaterials;
        }
    }
}
