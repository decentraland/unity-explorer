using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.ObjectHighlight
{
    public struct ObjectHighlightSettings
    {
        public Color Color;
        public float Width;
    }

    public partial class RenderFeature_ObjectHighlight : ScriptableRendererFeature
    {
        // The ContextItem used to store the texture reference at.
        public class TexRefData : ContextItem
        {
            // The texture reference variable.
            public TextureHandle textureColour = TextureHandle.nullHandle;
            public TextureHandle textureDepth = TextureHandle.nullHandle;

            // Reset function required by ContextItem. It should reset all variables not carried
            // over to next frame.
            public override void Reset()
            {
                // We should always reset texture handles since they are only vaild for the current frame.
                textureColour = TextureHandle.nullHandle;
                textureDepth = TextureHandle.nullHandle;
            }
        }

        private static readonly ReportData m_ReportData = new ("DCL_RenderFeature_Outline", ReportHint.SessionStatic);
        private static readonly Dictionary<Renderer, ObjectHighlightSettings> m_HighLightRenderers = new ();

        public static readonly IHighlightedObjects HighlightedObjects = new LogHighlightedObjects(
            new HighlightedObjects(m_HighLightRenderers)
        );

        // Input Pass Data
        private RenderPass_DrawObjects renderPass_DrawObjects = null!;
        public Material highlightInputMaterial;
        public Material highlightInputBlurMaterial;
        public Material highlightOutputMaterial;

        // Output Pass Data
        //private RenderPass_RenderResult renderPass_RenderResult;

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

            // if (highlightOutputMaterial != null)
            // {
            //     renderPass_RenderResult = new RenderPass_RenderResult(m_HighLightRenderers)
            //     {
            //         renderPassEvent = RenderPassEvent.AfterRenderingTransparents,
            //     };
            // }
        }

        public override void AddRenderPasses(ScriptableRenderer _renderer, ref RenderingData _renderingData)
        {
            if (renderPass_DrawObjects != null)
            {
                _renderer.EnqueuePass(renderPass_DrawObjects);
                //_renderer.EnqueuePass(renderPass_RenderResult);
            }
        }

        protected override void Dispose(bool _bDisposing)
        {

        }
    }
}
