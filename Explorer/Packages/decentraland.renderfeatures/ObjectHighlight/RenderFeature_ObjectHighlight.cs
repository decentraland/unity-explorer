using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.ObjectHighlight
{
    public struct ObjectHighlightSettings
    {
        public Color Color;
        public float Width;
    }

    public partial class RenderFeature_ObjectHighlight : ScriptableRendererFeature
    {
        private static readonly Dictionary<Renderer, ObjectHighlightSettings> m_HighLightRenderers = new ();

        public static readonly IHighlightedObjects HighlightedObjects = new HighlightedObjects(m_HighLightRenderers);

        // Pass Data
        private RenderPass_DrawObjects renderPass_DrawObjects = null!;
        public Material highlightInputMaterial;
        public Material highlightInputBlurMaterial;
        public Material highlightOutputMaterial;

        public RenderFeature_ObjectHighlight()
        {

        }

        public override void Create()
        {
            if (highlightInputMaterial != null && highlightInputBlurMaterial != null && highlightOutputMaterial  != null)
            {
                renderPass_DrawObjects = new RenderPass_DrawObjects(m_HighLightRenderers)
                {
                    renderPassEvent = RenderPassEvent.AfterRenderingTransparents,
                    m_highLightInputMaterial = highlightInputMaterial,
                    m_highlightInputBlurMaterial = highlightInputBlurMaterial,
                    m_highlightOutputMaterial = highlightOutputMaterial,
                };
            }
        }

        public override void AddRenderPasses(ScriptableRenderer _renderer, ref RenderingData _renderingData)
        {
            if (renderPass_DrawObjects != null)
            {
                _renderer.EnqueuePass(renderPass_DrawObjects);
            }
        }

        protected override void Dispose(bool _bDisposing)
        {

        }
    }
}
