using DCL.Diagnostics;
using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.Rendering.Avatar
{
    [Serializable]
    internal class OutlineRendererFeature_Settings
    {
        // Parameters
        [SerializeField] internal float OutlineThickness = 1.0f;
        [SerializeField] internal float DepthSensitivity = 0.05f;
        [SerializeField] internal float NormalsSensitivity = 1.0f;
        [SerializeField] internal float ColorSensitivity = 0.5f;
        [SerializeField] internal Color OutlineColor = new (1.0f, 1.0f, 1.0f, 0.5f);
    }

    public partial class OutlineRendererFeature : ScriptableRendererFeature
    {
        private const string k_ShaderName_DepthNormals = "Outline/DepthNormals";
        private const string k_ShaderName_Outline = "Avatar/Outline";
        private readonly ReportData m_ReportData = new ("DCL_RenderFeature_Outline", ReportHint.SessionStatic);

        [SerializeField] private OutlineRendererFeature_Settings m_Settings;

        // DepthNormals Pass Data
        private DepthNormalsRenderPass depthNormalsRenderPass;
        private Material depthNormalsMaterial;
        private Shader m_ShaderDepthNormals;
        private RTHandle depthNormalsRTHandle_Colour;
        private RTHandle depthNormalsRTHandle_Depth;
        private RenderTextureDescriptor depthNormalsRTDescriptor_Colour;
        private RenderTextureDescriptor depthNormalsRTDescriptor_Depth;

        // Outline Pass Data
        private OutlineRenderPass outlineRenderPass;
        private Material outlineMaterial;
        private Shader m_ShaderOutline;
        private RTHandle outlineRTHandle;
        private RenderTextureDescriptor outlineRTDescriptor;

        public OutlineRendererFeature()
        {
            m_Settings = new OutlineRendererFeature_Settings();
        }

        public override void Create()
        {
            depthNormalsRenderPass = new DepthNormalsRenderPass();
            depthNormalsRenderPass.renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;

            outlineRenderPass = new OutlineRenderPass();
            outlineRenderPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        public override void SetupRenderPasses(ScriptableRenderer _renderer, in RenderingData _renderingData)
        {
            // DepthNormals Material, Shader, RenderTarget and pass setups
            {
                if (depthNormalsMaterial == null)
                {
                    m_ShaderDepthNormals = Shader.Find(k_ShaderName_DepthNormals);

                    if (m_ShaderDepthNormals == null)
                    {
                        ReportHub.LogError(m_ReportData, "m_ShaderDepthNormals not found.");
                        return;
                    }

                    depthNormalsMaterial = CoreUtils.CreateEngineMaterial(m_ShaderDepthNormals);

                    if (depthNormalsMaterial == null)
                    {
                        ReportHub.LogError(m_ReportData, "depthNormalsMaterial not found.");
                        return;
                    }
                }

                // DepthNormals - Colour Texture
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
                    depthNormalsRTDescriptor_Colour = desc;
                    RenderingUtils.ReAllocateIfNeeded(ref depthNormalsRTHandle_Colour, depthNormalsRTDescriptor_Colour, FilterMode.Point, TextureWrapMode.Clamp, isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_DepthNormals_ColourTexture");
                }

                // DepthNormals - Depth Texture
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
                    depthNormalsRTDescriptor_Depth = desc;
                    RenderingUtils.ReAllocateIfNeeded(ref depthNormalsRTHandle_Depth, depthNormalsRTDescriptor_Depth, FilterMode.Point, TextureWrapMode.Clamp, isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_DepthNormals_DepthTexture");
                }

                depthNormalsRenderPass.Setup(depthNormalsMaterial, depthNormalsRTHandle_Colour, depthNormalsRTDescriptor_Colour, depthNormalsRTHandle_Depth, depthNormalsRTDescriptor_Depth);
            }

            // Outline Material, Shader, RenderTarget and pass setups
            {
                if (outlineMaterial == null)
                {
                    m_ShaderOutline = Shader.Find(k_ShaderName_Outline);

                    if (m_ShaderOutline == null)
                    {
                        ReportHub.LogError(m_ReportData, "m_ShaderOutline not found.");
                        return;
                    }

                    outlineMaterial = CoreUtils.CreateEngineMaterial(m_ShaderOutline);

                    if (outlineMaterial == null)
                    {
                        ReportHub.LogError(m_ReportData, "outlineMaterial not found.");
                        return;
                    }
                }

                outlineRTDescriptor = _renderingData.cameraData.cameraTargetDescriptor;
                outlineRTDescriptor.depthBufferBits = 0;
                RenderingUtils.ReAllocateIfNeeded(ref outlineRTHandle, outlineRTDescriptor, FilterMode.Point, TextureWrapMode.Clamp, isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_OutlineTexture");
                outlineRenderPass.Setup(m_Settings, outlineMaterial, outlineRTHandle, outlineRTDescriptor, depthNormalsRTHandle_Colour);
            }
        }

        public override void AddRenderPasses(ScriptableRenderer _renderer, ref RenderingData _renderingData)
        {
            // DepthNormals
            if (depthNormalsMaterial != null && m_ShaderDepthNormals != null && depthNormalsRTHandle_Colour != null) { _renderer.EnqueuePass(depthNormalsRenderPass); }

            // Outline
            if (outlineMaterial != null && m_ShaderOutline != null && outlineRTHandle != null) { _renderer.EnqueuePass(outlineRenderPass); }
        }

        protected override void Dispose(bool _bDisposing)
        {
            // DepthNormals cleanup
            {
                depthNormalsRenderPass?.Dispose();
                depthNormalsRTHandle_Colour?.Release();
                depthNormalsRTHandle_Depth?.Release();
            }

            // Outline cleanup
            {
                outlineRenderPass?.Dispose();
                outlineRTHandle?.Release();
            }
        }
    }
}
