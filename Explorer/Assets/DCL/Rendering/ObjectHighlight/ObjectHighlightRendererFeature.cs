using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DCL.Rendering.ObjectHighlight
{
    public struct ObjectHighlightSettings
    {
        public Color Color;

        /// <summary>
        ///     Outline width in pixels.
        /// </summary>
        public float Width;

        /// <summary>
        ///     Opacity applied across the whole visible surface, before the rim is added.
        /// </summary>
        public float Fill;

        /// <summary>
        ///     Opacity added at grazing angles, on top of <see cref="Fill" />.
        /// </summary>
        public float Rim;

        /// <summary>
        ///     Falloff exponent of the rim. Higher values tighten it against the silhouette.
        /// </summary>
        public float FresnelPower;

        /// <summary>
        ///     Ceiling on the combined fill and rim, so the highlight tints the object rather than
        ///     replacing it.
        /// </summary>
        public float MaxOpacity;

        /// <summary>
        ///     Multiplier on the surface opacity, driving the breathing effect.
        /// </summary>
        public float Pulse;

        /// <summary>
        ///     Tolerance in metres before the surface counts as hidden behind other geometry.
        /// </summary>
        public float SurfaceDepthBias;

        /// <summary>
        ///     Tolerance in metres before the outline counts as hidden. Deliberately far looser than
        ///     <see cref="SurfaceDepthBias" />: the outline is drawn outside the silhouette, over ground and
        ///     walls whose depth differs from the object's own.
        /// </summary>
        public float OutlineDepthBias;
    }

    /// <summary>
    ///     Draws an outline and a surface treatment over renderers registered in
    ///     <see cref="HIGHLIGHTED_OBJECTS" />. Registrations are cleared every frame, so callers re-register
    ///     for as long as they want an object highlighted.
    /// </summary>
    public partial class ObjectHighlightRendererFeature : ScriptableRendererFeature
    {
        private static readonly Dictionary<Renderer, ObjectHighlightSettings> HIGHLIGHT_RENDERERS = new ();
        private static readonly Dictionary<Renderer, ObjectHighlightSettings> HIGHLIGHT_RENDERERS_AVATAR = new ();

        public static readonly IHighlightedObjects HIGHLIGHTED_OBJECTS = new HighlightedObjects(HIGHLIGHT_RENDERERS);
        public static readonly IHighlightedObjects HIGHLIGHTED_OBJECTS_AVATAR = new HighlightedObjects(HIGHLIGHT_RENDERERS_AVATAR);

        [SerializeField] private Material? inputMaterial;
        [SerializeField] private Material? blurMaterial;
        [SerializeField] private Material? outputMaterial;

        private DrawObjectsPass? renderPass;

        public override void Create()
        {
            renderPass = null;

            if (inputMaterial == null || blurMaterial == null || outputMaterial == null)
                return;

            renderPass = new DrawObjectsPass(HIGHLIGHT_RENDERERS, HIGHLIGHT_RENDERERS_AVATAR, inputMaterial, blurMaterial, outputMaterial)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents,
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderPass != null)
                renderer.EnqueuePass(renderPass);
        }

        protected override void Dispose(bool disposing)
        {
            renderPass?.Dispose();
            renderPass = null;
        }
    }
}
