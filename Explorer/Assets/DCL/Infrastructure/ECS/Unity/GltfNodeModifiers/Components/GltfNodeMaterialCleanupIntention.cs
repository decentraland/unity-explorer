using Arch.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.GltfNodeModifiers.Components
{
    /// <summary>
    ///     Component to mark GltfNode entities that require material cleanup
    ///     Added when a GltfNode entity with PBMaterial is being removed
    /// </summary>
    public struct GltfNodeMaterialCleanupIntention
    {
        /// <summary>
        ///     List of Unity Renderer components that need material reset
        /// </summary>
        public List<Renderer> Renderers { get; set; }
        
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