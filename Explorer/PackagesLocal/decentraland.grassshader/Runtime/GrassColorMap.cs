using UnityEngine;

namespace StylizedGrass
{
    /// <summary>
    ///     Data asset produced by <see cref="GrassColorMapRenderer" />. Holds the baked top-down
    ///     ground-colour texture and the world-space bounds it was baked over, so the grass and
    ///     terrain shaders can look up a coherent per-blade ground tint from a world XZ position.
    ///     <see cref="SetActive" /> publishes them as the shader globals <c>_ColorMap</c>,
    ///     <c>_ColorMapBounds</c> and <c>_ColorMap_TexelSize</c>: one bake reaches every grass,
    ///     flower and hedge material at once. The consuming shaders therefore must not declare those
    ///     three names in their Properties block, because a material property of the same name
    ///     shadows the global with a per-material value that nothing ever writes.
    /// </summary>
    [CreateAssetMenu(fileName = "GrassColorMap", menuName = "Decentraland/Stylized Grass/Color Map")]
    public class GrassColorMap : ScriptableObject
    {
        // Shader globals sampled by StylizedGrass.shader (and any terrain material that tints
        // against the same map). Kept in sync with the uniform names declared in the shaders.
        private static readonly int COLOR_MAP_ID = Shader.PropertyToID("_ColorMap");
        private static readonly int COLOR_MAP_BOUNDS_ID = Shader.PropertyToID("_ColorMapBounds");
        private static readonly int COLOR_MAP_TEXEL_SIZE_ID = Shader.PropertyToID("_ColorMap_TexelSize");

        [Tooltip("The baked ground-colour texture, indexed by world XZ through 'bounds'.")]
        public Texture2D texture;

        [Tooltip("World-space region the colour map covers. XZ min/size are packed into _ColorMapBounds.")]
        public Bounds bounds;

        /// <summary>
        ///     The color map whose bake the shader globals currently carry: the last one to call
        ///     <see cref="SetActive" />. Lets a renderer that is torn down tell whether the globals
        ///     still point at its texture or a newer bake has already replaced them.
        /// </summary>
        public static GrassColorMap Active { get; private set; }

        /// <summary>
        ///     The (minX, minZ, sizeX, sizeZ) packing published to <c>_ColorMapBounds</c>. Both the
        ///     grass shader and the color-map bake use this exact layout to convert a world XZ
        ///     position into a 0..1 colour-map UV.
        /// </summary>
        public Vector4 BoundsVector =>
            new (bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z);

        public bool IsActive => Active == this;

        /// <summary>
        ///     Push this color map to the shader globals so grass and terrain sample it. Safe to call
        ///     repeatedly; a null texture publishes Unity's built-in white so grass renders untinted
        ///     rather than sampling stale data.
        /// </summary>
        public void SetActive()
        {
            Texture2D published = texture != null ? texture : Texture2D.whiteTexture;

            Shader.SetGlobalTexture(COLOR_MAP_ID, published);
            Shader.SetGlobalVector(COLOR_MAP_BOUNDS_ID, BoundsVector);

            // A global texture gets no companion _TexelSize the way a material texture property
            // does, so the half-texel inset the shaders apply when sampling has to be published too.
            Shader.SetGlobalVector(COLOR_MAP_TEXEL_SIZE_ID,
                new Vector4(1f / published.width, 1f / published.height, published.width, published.height));

            Active = this;
        }
    }
}
