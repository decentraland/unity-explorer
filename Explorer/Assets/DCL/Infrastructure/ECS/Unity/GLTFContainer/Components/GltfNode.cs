using Arch.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.GLTFContainer.Components
{
    /// <summary>
    ///     Component to mark entities that represent GLTF nodes and hold references to their Unity components
    /// </summary>
    public struct GltfNode
    {
        /// <summary>
        ///     List of Unity Renderer components for the GLTF node(s)
        ///     For specific path modifiers: contains one renderer
        ///     For global modifiers (path=""): contains all renderers in the GLTF asset
        /// </summary>
        public List<Renderer> Renderers { get; set; }
        
        /// <summary>
        ///     Reference to the entity that contains the GltfContainerComponent
        /// </summary>
        public Entity ContainerEntity { get; set; }
        
        /// <summary>
        ///     The original path from the modifier that this node represents
        ///     For global modifiers: empty string or null
        ///     For specific modifiers: the path used to find the renderer
        /// </summary>
        public string? Path { get; set; }
    }
} 