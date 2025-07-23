using Arch.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace ECS.Unity.GltfNodeModifiers.Components
{
    public struct GltfNodeModifiers
    {
        /// <summary>
        ///     Collection of entities created for GLTF nodes that have modifiers applied
        /// </summary>
        public readonly List<Entity> GltfNodeEntities;

        /// <summary>
        ///     Dictionary storing the original materials for each renderer before modifications
        /// </summary>
        public readonly Dictionary<Renderer, Material> OriginalMaterials;

        public GltfNodeModifiers(List<Entity> gltfNodeEntities)
        {
            GltfNodeEntities = gltfNodeEntities;
            OriginalMaterials = DictionaryPool<Renderer, Material>.Get();
        }
    }
}
