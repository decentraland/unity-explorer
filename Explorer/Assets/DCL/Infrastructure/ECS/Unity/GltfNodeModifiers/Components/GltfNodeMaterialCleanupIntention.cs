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
        public IReadOnlyList<Renderer> Renderers { get; set; }

        /// <summary>
        ///     Reference to the entity that contains the GltfContainerComponent
        /// </summary>
        public Entity ContainerEntity { get; set; }

        /// <summary>
        ///     Whether the entity should be destroyed after material cleanup
        /// </summary>
        public bool Destroy { get; set; }
    }
}
