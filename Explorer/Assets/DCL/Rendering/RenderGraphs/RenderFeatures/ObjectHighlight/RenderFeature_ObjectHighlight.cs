using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule.Util;

// ReSharper disable InconsistentNaming

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
            public TextureHandle texture = TextureHandle.nullHandle;

            // Reset function required by ContextItem. It should reset all variables not carried
            // over to next frame.
            public override void Reset()
            {
                // We should always reset texture handles since they are only vaild for the current frame.
                texture = TextureHandle.nullHandle;
            }
        }

        private const string k_ShaderName_HighlightInput = "DCL/HighlightInput_Override";
        private const string k_ShaderName_HighlightInputBlur = "DCL/HighlightInput_Blur";
        private const string k_ShaderName_HighlightOutput = "DCL/HighlightOutput";
        private static readonly ReportData m_ReportData = new ("DCL_RenderFeature_Outline", ReportHint.SessionStatic);

        private static readonly Dictionary<Renderer, ObjectHighlightSettings> m_HighLightRenderers = new ();

        public static readonly IObjectHighlight HighlightedObjects = new LogObjectHighlight(
            new ObjectHighlight(m_HighLightRenderers)
        );

        // Input Pass Data
        private RenderPass_DrawObjects highlightInputRenderPass = null!;
        private Material highlightInputMaterial;
        private Material highlightInputBlurMaterial;
        private Shader m_ShaderHighlightInput;
        private Shader m_ShaderHighlightInputBlur;
        // private RTHandle highlightRTHandle_Colour;
        // private RTHandle highlightRTHandle_Depth;
        // private RTHandle highlightRTHandle_Colour_Blur_Ping;
        // private RTHandle highlightRTHandle_Colour_Blur_Pong;
        // private RenderTextureDescriptor highlightRTDescriptor_Colour;
        // private RenderTextureDescriptor highlightRTDescriptor_Depth;
        // private RenderTextureDescriptor highlightRTDescriptor_Colour_Blur;

        // Output Pass Data
        private RenderPass_RenderResult highlightOutputRenderPass;
        private Material? highlightOutputMaterial;
        private Shader? m_ShaderHighlightOutput;

        public RenderFeature_ObjectHighlight()
        {
            highlightInputMaterial = null;
            highlightInputBlurMaterial = null;
            m_ShaderHighlightInput = null;
            m_ShaderHighlightInputBlur = null;
        }

        public override void Create()
        {
            if (highlightInputMaterial == null)
            {
                m_ShaderHighlightInput = Shader.Find(k_ShaderName_HighlightInput);

                if (m_ShaderHighlightInput == null)
                {
                    ReportHub.LogError(m_ReportData, "m_ShaderHighlightInput not found.");
                    return;
                }

                highlightInputMaterial = CoreUtils.CreateEngineMaterial(m_ShaderHighlightInput);

                if (highlightInputMaterial == null)
                {
                    ReportHub.LogError(m_ReportData, "highlightInputMaterial not found.");
                    return;
                }
            }

            if (highlightInputBlurMaterial == null)
            {
                m_ShaderHighlightInputBlur = Shader.Find(k_ShaderName_HighlightInputBlur);

                if (m_ShaderHighlightInputBlur == null)
                {
                    ReportHub.LogError(m_ReportData, "m_ShaderHighlightInputBlur not found.");
                    return;
                }

                highlightInputBlurMaterial = CoreUtils.CreateEngineMaterial(m_ShaderHighlightInputBlur);

                if (highlightInputBlurMaterial == null)
                {
                    ReportHub.LogError(m_ReportData, "highlightInputBlurMaterial not found.");
                    return;
                }
            }

            if (highlightInputBlurMaterial != null && highlightInputMaterial != null)
            {
                highlightInputRenderPass = new RenderPass_DrawObjects(m_HighLightRenderers)
                {
                    renderPassEvent = RenderPassEvent.AfterRenderingPrePasses,
                };
            }

            if (highlightOutputMaterial == null)
            {
                m_ShaderHighlightOutput = Shader.Find(k_ShaderName_HighlightOutput);

                if (m_ShaderHighlightOutput == null)
                {
                    ReportHub.LogError(m_ReportData, "m_ShaderHighlightOutput not found.");
                    return;
                }

                highlightOutputMaterial = CoreUtils.CreateEngineMaterial(m_ShaderHighlightOutput);

                if (highlightOutputMaterial == null)
                {
                    ReportHub.LogError(m_ReportData, "highlightOutputMaterial not found.");
                    return;
                }
            }

            if (highlightOutputMaterial != null)
            {
                highlightOutputRenderPass = new RenderPass_RenderResult(m_HighLightRenderers)
                {
                    renderPassEvent = RenderPassEvent.AfterRenderingTransparents,
                };
            }
        }

        public override void AddRenderPasses(ScriptableRenderer _renderer, ref RenderingData _renderingData)
        {
            // Highlight Input
            if (highlightInputMaterial != null && m_ShaderHighlightInput != null)
            {
                _renderer.EnqueuePass(highlightInputRenderPass);
            }

            // HighLight Output
            if (highlightOutputMaterial != null && m_ShaderHighlightOutput != null)
            {
                _renderer.EnqueuePass(highlightOutputRenderPass);
            }
        }

        protected override void Dispose(bool _bDisposing)
        {

        }
    }
}
