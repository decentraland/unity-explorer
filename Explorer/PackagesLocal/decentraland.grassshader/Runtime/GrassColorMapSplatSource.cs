using System;
using UnityEngine;

namespace StylizedGrass
{
    /// <summary>
    ///     A ground described by splat data instead of a scene object: a layer-weight control map
    ///     laid over a world-space region plus up to four tiled diffuse layers.
    ///     <see cref="GrassColorMapRenderer" /> bakes it exactly like a Unity Terrain, so a
    ///     GPU-generated ground with no Terrain or Renderer component still contributes its colour
    ///     to the map. Tiling follows the Unity terrain-layer convention:
    ///     <c>uv = (worldXZ + tileOffset) / tileSize</c>, for the control map and each layer alike.
    /// </summary>
    [Serializable]
    public sealed class GrassColorMapSplatSource
    {
        public const int MAX_LAYERS = 4;

        [Serializable]
        public struct Layer
        {
            public Texture diffuse;
            public Vector2 tileSize;
            public Vector2 tileOffset;

            public Layer(Texture diffuse, Vector2 tileSize, Vector2 tileOffset)
            {
                this.diffuse = diffuse;
                this.tileSize = tileSize;
                this.tileOffset = tileOffset;
            }
        }

        [Tooltip("World-space region this ground covers. Only the XZ extents take part in the bake.")]
        public Bounds bounds;

        [Tooltip("Layer weights, RGBA = layers 0..3, sampled with the control tiling below.")]
        public Texture control;

        [Tooltip("World metres per control-map repeat. Equal to the region size to stretch the map across it once.")]
        public Vector2 controlTileSize = Vector2.one;

        public Vector2 controlTileOffset;

        [Tooltip("Diffuse texture per layer, in control-channel order. Layers past the fourth are ignored.")]
        public Layer[] layers = Array.Empty<Layer>();

        /// <summary>
        ///     Layers the bake actually blends: the control channels beyond this count carry no
        ///     colour and no coverage.
        /// </summary>
        public int LayerCount => Mathf.Min(layers != null ? layers.Length : 0, MAX_LAYERS);
    }
}
