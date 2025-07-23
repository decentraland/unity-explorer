using Arch.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.GltfNodeModifiers.Components
{
    public struct GltfNodeMaterialCleanupIntention
    {
        /// <summary>
        ///     Collection of Unity Renderer components that need material reset
        /// </summary>
        public readonly IReadOnlyList<Renderer> Renderers;

        /// <summary>
        ///     Reference to the entity that contains the GltfContainerComponent
        /// </summary>
        public readonly Entity ContainerEntity;

        /// <summary>
        ///     Whether the entity should be destroyed after material cleanup
        /// </summary>
        public readonly bool Destroy;

        public GltfNodeMaterialCleanupIntention(IReadOnlyList<Renderer> renderers, Entity containerEntity, bool destroy = false)
        {
            Renderers = renderers;
            ContainerEntity = containerEntity;
            Destroy = destroy;
        }
    }
}
