﻿using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.Rendering.Highlight
{
    public struct HighlightSettings
    {
        public Color Color;
        public float Width;
        public Vector3 Offset;
    }

    [Serializable]
    internal class HighlightRendererFeature_Settings
    {
        // Parameters
        [SerializeField] internal float OutlineThickness = 1.0f;
        [SerializeField] internal float DepthSensitivity = 0.05f;
        [SerializeField] internal float NormalsSensitivity = 1.0f;
        [SerializeField] internal float ColorSensitivity = 0.5f;
        [SerializeField] internal Color OutlineColor = new (1.0f, 1.0f, 1.0f, 0.5f);
    }

    public partial class HighlightRendererFeature : ScriptableRendererFeature
    {
        private const string k_ShaderName_HighlightInput = "DCL/HighlightInput_Override";
        private const string k_ShaderName_HighlightInputBlur = "DCL/HighlightInput_Blur";
        private const string k_ShaderName_HighlightInputBlooming = "DCL/HighlightInput_Blooming";
        private const string k_ShaderName_HighlightOutput = "DCL/HighlightOutput";
        private readonly ReportData m_ReportData = new ("DCL_RenderFeature_Outline", ReportHint.SessionStatic);

        [SerializeField] private HighlightRendererFeature_Settings m_Settings;
        public static Dictionary<Renderer, HighlightSettings> m_HighLightRenderers;

        // Input Pass Data
        private HighlightInputRenderPass highlightInputRenderPass;
        private Material highlightInputMaterial;
        private Material highlightInputBlurMaterial;
        private Material highlightInputBloomingMaterial;
        private Shader m_ShaderHighlightInput;
        private Shader m_ShaderHighlightInputBlur;
        private Shader m_ShaderHighlightInputBlooming;
        private RTHandle highlightRTHandle_Colour;
        private RTHandle highlightRTHandle_Depth;
        private RTHandle highlightRTHandle_Colour_Blur_Ping;
        private RTHandle highlightRTHandle_Colour_Blur_Pong;
        private RTHandle highlightRTHandle_Colour_Blooming_0_Down;
        private RTHandle highlightRTHandle_Colour_Blooming_0_Up;
        private RTHandle highlightRTHandle_Colour_Blooming_1_Down;
        private RTHandle highlightRTHandle_Colour_Blooming_1_Up;
        private RTHandle highlightRTHandle_Colour_Blooming_2_Down;
        private RTHandle highlightRTHandle_Colour_Blooming_2_Up;
        private RTHandle highlightRTHandle_Colour_Blooming_3_Down;
        private RTHandle highlightRTHandle_Colour_Blooming_3_Up;
        private RTHandle highlightRTHandle_Colour_Blooming_4_Down;
        private RTHandle highlightRTHandle_Colour_Blooming_4_Up;
        private RTHandle highlightRTHandle_Colour_Bloom;
        private RenderTextureDescriptor highlightRTDescriptor_Colour;
        private RenderTextureDescriptor highlightRTDescriptor_Depth;
        private RenderTextureDescriptor highlightRTDescriptor_Colour_Blur;

        // Output Pass Data
        private HighlightOutputRenderPass highlightOutputRenderPass;
        private Material highlightOutputMaterial;
        private Shader m_ShaderHighlightOutput;

        public HighlightRendererFeature()
        {
            m_Settings = new HighlightRendererFeature_Settings();
            m_HighLightRenderers = new Dictionary<Renderer, HighlightSettings>();
        }

        public override void Create()
        {
            highlightInputRenderPass = new HighlightInputRenderPass(m_HighLightRenderers);
            highlightInputRenderPass.renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;

            highlightOutputRenderPass = new HighlightOutputRenderPass(m_HighLightRenderers);
            highlightOutputRenderPass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        public override void SetupRenderPasses(ScriptableRenderer _renderer, in RenderingData _renderingData)
        {
            // Highlight Input - Override Material & Shader, Colour & Depth Render Targets and pass setups
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

                if (highlightInputBloomingMaterial == null)
                {
                    m_ShaderHighlightInputBlooming = Shader.Find(k_ShaderName_HighlightInputBlooming);

                    if (m_ShaderHighlightInputBlooming == null)
                    {
                        ReportHub.LogError(m_ReportData, "m_ShaderHighlightInputBlooming not found.");
                        return;
                    }

                    highlightInputBloomingMaterial = CoreUtils.CreateEngineMaterial(m_ShaderHighlightInputBlooming);

                    if (highlightInputBloomingMaterial == null)
                    {
                        ReportHub.LogError(m_ReportData, "highlightInputBloomingMaterial not found.");
                        return;
                    }
                }

                // Highlight - Colour Texture
                {
                    var desc = new RenderTextureDescriptor();
                    desc.autoGenerateMips = false;
                    desc.bindMS = false;
                    desc.colorFormat = RenderTextureFormat.ARGB32;
                    desc.depthBufferBits = 0;
                    desc.depthStencilFormat = GraphicsFormat.None;
                    desc.dimension = TextureDimension.Tex2D;
                    desc.enableRandomWrite = false;
                    desc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
                    desc.height = _renderingData.cameraData.cameraTargetDescriptor.height;
                    desc.memoryless = RenderTextureMemoryless.None;
                    desc.mipCount = 0;
                    desc.msaaSamples = 1;
                    desc.shadowSamplingMode = ShadowSamplingMode.None;
                    desc.sRGB = false;
                    desc.stencilFormat = GraphicsFormat.None;
                    desc.useDynamicScale = false;
                    desc.useMipMap = false;
                    desc.volumeDepth = 0;
                    desc.vrUsage = VRTextureUsage.None;
                    desc.width = _renderingData.cameraData.cameraTargetDescriptor.width;
                    highlightRTDescriptor_Colour = desc;
                    RenderingUtils.ReAllocateIfNeeded(ref highlightRTHandle_Colour, highlightRTDescriptor_Colour, FilterMode.Bilinear, TextureWrapMode.Clamp, isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_Highlight_ColourTexture");
                    RenderingUtils.ReAllocateIfNeeded(ref highlightRTHandle_Colour_Bloom, highlightRTDescriptor_Colour, FilterMode.Bilinear, TextureWrapMode.Clamp, isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_Highlight_ColourTexture_Bloom");
                }

                // Highlight - Depth Texture
                {
                    var desc = new RenderTextureDescriptor();
                    desc.autoGenerateMips = false;
                    desc.bindMS = false;
                    desc.colorFormat = RenderTextureFormat.Shadowmap;
                    desc.depthBufferBits = 32;
                    desc.depthStencilFormat = GraphicsFormat.D32_SFloat;
                    desc.dimension = TextureDimension.Tex2D;
                    desc.enableRandomWrite = false;
                    desc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
                    desc.height = _renderingData.cameraData.cameraTargetDescriptor.height;
                    desc.memoryless = RenderTextureMemoryless.None;
                    desc.mipCount = 0;
                    desc.msaaSamples = 1;
                    desc.shadowSamplingMode = ShadowSamplingMode.None;
                    desc.sRGB = false;
                    desc.stencilFormat = GraphicsFormat.None;
                    desc.useDynamicScale = false;
                    desc.useMipMap = false;
                    desc.volumeDepth = 0;
                    desc.vrUsage = VRTextureUsage.None;
                    desc.width = _renderingData.cameraData.cameraTargetDescriptor.width;
                    highlightRTDescriptor_Depth = desc;
                    RenderingUtils.ReAllocateIfNeeded(ref highlightRTHandle_Depth, highlightRTDescriptor_Depth, FilterMode.Point, TextureWrapMode.Clamp, isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_Highlight_DepthTexture");
                }

                // Highlight - Blur Texture (PING & PONG)
                {
                    var desc = new RenderTextureDescriptor();
                    desc.autoGenerateMips = false;
                    desc.bindMS = false;
                    desc.colorFormat = RenderTextureFormat.ARGB32;
                    desc.depthBufferBits = 0;
                    desc.depthStencilFormat = GraphicsFormat.None;
                    desc.dimension = TextureDimension.Tex2D;
                    desc.enableRandomWrite = false;
                    desc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
                    desc.height = _renderingData.cameraData.cameraTargetDescriptor.height;
                    desc.memoryless = RenderTextureMemoryless.None;
                    desc.mipCount = 0;
                    desc.msaaSamples = 1;
                    desc.shadowSamplingMode = ShadowSamplingMode.None;
                    desc.sRGB = false;
                    desc.stencilFormat = GraphicsFormat.None;
                    desc.useDynamicScale = false;
                    desc.useMipMap = false;
                    desc.volumeDepth = 0;
                    desc.vrUsage = VRTextureUsage.None;
                    desc.width = _renderingData.cameraData.cameraTargetDescriptor.width;
                    highlightRTDescriptor_Colour_Blur = desc;
                    RenderingUtils.ReAllocateIfNeeded(ref highlightRTHandle_Colour_Blur_Ping, highlightRTDescriptor_Colour_Blur, FilterMode.Bilinear, TextureWrapMode.Clamp, isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_Highlight_ColourTexture_Blur_Ping");
                    RenderingUtils.ReAllocateIfNeeded(ref highlightRTHandle_Colour_Blur_Pong, highlightRTDescriptor_Colour_Blur, FilterMode.Bilinear, TextureWrapMode.Clamp, isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_Highlight_ColourTexture_Blur_Pong");
                }

                // Highlight - Blooming Texture
                {
                    var desc = new RenderTextureDescriptor();
                    desc.autoGenerateMips = false;
                    desc.bindMS = false;
                    desc.colorFormat = RenderTextureFormat.ARGB32;
                    desc.depthBufferBits = 0;
                    desc.depthStencilFormat = GraphicsFormat.None;
                    desc.dimension = TextureDimension.Tex2D;
                    desc.enableRandomWrite = false;
                    desc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
                    desc.height = (int)(_renderingData.cameraData.cameraTargetDescriptor.height * 0.5f);
                    desc.memoryless = RenderTextureMemoryless.None;
                    desc.mipCount = 0;
                    desc.msaaSamples = 1;
                    desc.shadowSamplingMode = ShadowSamplingMode.None;
                    desc.sRGB = false;
                    desc.stencilFormat = GraphicsFormat.None;
                    desc.useDynamicScale = false;
                    desc.useMipMap = false;
                    desc.volumeDepth = 0;
                    desc.vrUsage = VRTextureUsage.None;
                    desc.width = (int)(_renderingData.cameraData.cameraTargetDescriptor.width * 0.5f);
                    RenderingUtils.ReAllocateIfNeeded(ref highlightRTHandle_Colour_Blooming_0_Down, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_Highlight_ColourTexture_Blooming_0_Down");
                    RenderingUtils.ReAllocateIfNeeded(ref highlightRTHandle_Colour_Blooming_0_Up, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_Highlight_ColourTexture_Blooming_0_Up");
                    desc.height = (int)(desc.height * 0.5f);
                    desc.width = (int)(desc.width * 0.5f);
                    RenderingUtils.ReAllocateIfNeeded(ref highlightRTHandle_Colour_Blooming_1_Down, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_Highlight_ColourTexture_Blooming_1_Down");
                    RenderingUtils.ReAllocateIfNeeded(ref highlightRTHandle_Colour_Blooming_1_Up, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_Highlight_ColourTexture_Blooming_1_Up");
                    desc.height = (int)(desc.height * 0.5f);
                    desc.width = (int)(desc.width * 0.5f);
                    RenderingUtils.ReAllocateIfNeeded(ref highlightRTHandle_Colour_Blooming_2_Down, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_Highlight_ColourTexture_Blooming_2_Down");
                    RenderingUtils.ReAllocateIfNeeded(ref highlightRTHandle_Colour_Blooming_2_Up, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_Highlight_ColourTexture_Blooming_2_Up");
                    desc.height = (int)(desc.height * 0.5f);
                    desc.width = (int)(desc.width * 0.5f);
                    RenderingUtils.ReAllocateIfNeeded(ref highlightRTHandle_Colour_Blooming_3_Down, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_Highlight_ColourTexture_Blooming_3_Down");
                    RenderingUtils.ReAllocateIfNeeded(ref highlightRTHandle_Colour_Blooming_3_Up, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_Highlight_ColourTexture_Blooming_3_Up");
                    desc.height = (int)(desc.height * 0.5f);
                    desc.width = (int)(desc.width * 0.5f);
                    RenderingUtils.ReAllocateIfNeeded(ref highlightRTHandle_Colour_Blooming_4_Down, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_Highlight_ColourTexture_Blooming_4_Down");
                    RenderingUtils.ReAllocateIfNeeded(ref highlightRTHandle_Colour_Blooming_4_Up, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_Highlight_ColourTexture_Blooming_4_Up");
                }

                highlightInputRenderPass.Setup(highlightInputMaterial, highlightInputBlurMaterial, highlightInputBloomingMaterial,
                                            highlightRTHandle_Colour, highlightRTDescriptor_Colour,
                                            highlightRTHandle_Depth, highlightRTDescriptor_Depth,
                                            highlightRTHandle_Colour_Blur_Ping, highlightRTHandle_Colour_Blur_Pong, highlightRTDescriptor_Colour_Blur,
                                            highlightRTHandle_Colour_Blooming_0_Down, highlightRTHandle_Colour_Blooming_1_Down, highlightRTHandle_Colour_Blooming_2_Down, highlightRTHandle_Colour_Blooming_3_Down, highlightRTHandle_Colour_Blooming_4_Down,
                                            highlightRTHandle_Colour_Blooming_0_Up, highlightRTHandle_Colour_Blooming_1_Up, highlightRTHandle_Colour_Blooming_2_Up, highlightRTHandle_Colour_Blooming_3_Up, highlightRTHandle_Colour_Blooming_4_Up,
                                            highlightRTHandle_Colour_Bloom);
            }

            // Highlight Output Material, Shader, RenderTarget and pass setups
            {
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

                highlightOutputRenderPass.Setup(m_Settings, highlightOutputMaterial, highlightRTHandle_Colour, highlightRTDescriptor_Colour);
            }
        }

        public override void AddRenderPasses(ScriptableRenderer _renderer, ref RenderingData _renderingData)
        {
            // Highlight Input
            if (highlightInputMaterial != null && m_ShaderHighlightInput != null && highlightRTHandle_Colour != null) { _renderer.EnqueuePass(highlightInputRenderPass); }

            // HighLight Output
            if (highlightOutputMaterial != null && m_ShaderHighlightOutput != null) { _renderer.EnqueuePass(highlightOutputRenderPass); }
        }

        protected override void Dispose(bool _bDisposing)
        {
            // Highlight Input cleanup
            {
                highlightInputRenderPass?.Dispose();
                highlightRTHandle_Colour?.Release();
                highlightRTHandle_Depth?.Release();
            }

            // HighLight Output cleanup
            {
                highlightOutputRenderPass?.Dispose();
                //outlineRTHandle?.Release();
            }
        }
    }
}
