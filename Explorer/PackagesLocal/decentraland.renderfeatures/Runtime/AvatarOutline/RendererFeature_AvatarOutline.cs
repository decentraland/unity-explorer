using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.AvatarOutline
{
    /// <summary>
    ///     Draws a cel-style outline around the avatars the gameplay layer flags each
    ///     frame. <see cref="AvatarShapeVisibilitySystem"/> fills
    ///     <see cref="m_AvatarOutlineRenderers"/> with the frustum-visible avatar
    ///     renderers; the pass consumes and clears the list every frame.
    ///
    ///     Technique: an inverted-hull outline. The flagged renderers are drawn
    ///     back-faces-only, extruded along their normals, in a flat colour, before the
    ///     opaque queue — the opaque avatar pass then overdraws the interior, leaving
    ///     the rim. This is self-contained (no dependency on the avatar material owning
    ///     an outline pass) and Vulkan-safe (no fullscreen blit / mask RT).
    /// </summary>
    public class RendererFeature_AvatarOutline : ScriptableRendererFeature
    {
        /// <summary>
        ///     Per-frame bucket of renderers to outline. Filled by
        ///     <c>AvatarShapeVisibilitySystem</c> (also read by reflection in the
        ///     avatar performance tests, which require this exact
        ///     <c>public static</c> field) and cleared by the draw pass.
        /// </summary>
        public static readonly List<Renderer> m_AvatarOutlineRenderers = new (256);

        [SerializeField] public Color outlineColor = new (0.05f, 0.6f, 1f, 1f);

        [Range(0.5f, 24f)]
        [SerializeField] public float outlineThickness = 4f;

        // The renderer asset binds the outline shader here; that reference is what
        // carries the shader into player builds, where nothing else references it
        // and Shader.Find would come back empty.
        [SerializeField] public Shader outlineShader;

        private const string OUTLINE_SHADER = "Hidden/DCL/RenderFeatures/AvatarOutline";
        private static readonly int s_OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int s_OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");

        private Material outlineMaterial;
        private RenderPass_OutlineDraw outlineDrawPass;

        public override void Create()
        {
            // Unity re-runs Create on every domain reload and on every inspector
            // edit of the renderer asset; release what the previous run owned first.
            Dispose(true);

            name = "RendererFeature_AvatarOutline";

            Shader shader = outlineShader != null ? outlineShader : Shader.Find(OUTLINE_SHADER);
            if (shader == null)
            {
                Debug.LogWarning($"[RendererFeature_AvatarOutline] shader '{OUTLINE_SHADER}' not found; avatar outline disabled.");
                return;
            }

            outlineMaterial = CoreUtils.CreateEngineMaterial(shader);
            outlineMaterial.SetColor(s_OutlineColorId, outlineColor);
            outlineMaterial.SetFloat(s_OutlineThicknessId, outlineThickness);

            outlineDrawPass = new RenderPass_OutlineDraw(m_AvatarOutlineRenderers, outlineMaterial)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingOpaques,
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (outlineDrawPass == null)
                return;

            CameraType camType = renderingData.cameraData.cameraType;
            if (camType == CameraType.Preview || camType == CameraType.Reflection)
                return;

            renderer.EnqueuePass(outlineDrawPass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(outlineMaterial);
            outlineMaterial = null;
            outlineDrawPass = null;
        }
    }
}
