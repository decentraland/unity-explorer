using DCL.Optimization.Pools;
using System;
using TMPro;
using UnityEngine;

namespace DCL.SDKComponents.TextShape.Component
{
    public struct TextShapeComponent : IPoolableComponentProvider<TextMeshPro>
    {
        public readonly TextMeshPro TextMeshPro;

        public TextMeshPro PoolableComponent => TextMeshPro;
        public Type PoolableComponentType => typeof(TextMeshPro);

        /// <summary>
        /// Whether the bounding box of the text shape is fully contained in the boundaries of the scene it belongs to.
        /// </summary>
        public bool IsContainedInScene;

        /// <summary>
        /// The size of the bounding box of the text the last time it was enabled (when disable, it equals zero).
        /// </summary>
        public Vector3 LastValidBoundingBoxSize;

        public TextShapeComponent(TextMeshPro textShape)
        {
            TextMeshPro = textShape;
            LastValidBoundingBoxSize = textShape.renderer.bounds.size; // Note: Using Renderer because the bounds of the TMP does not return what we need
            IsContainedInScene = false;
        }

        public void Dispose() { }
    }
}
