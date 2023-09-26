using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Diagnostics.ReportsHandling;
using Unity.VisualScripting;
using UnityEngine.Experimental.Rendering;

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
        [SerializeField] internal Color OutlineColor = new Color(1.0f, 1.0f, 1.0f, 0.5f);
    }
    public partial class OutlineRendererFeature : ScriptableRendererFeature
    {
        private ReportData m_ReportData = new ReportData("DCL_RenderFeature_Outline", ReportHint.SessionStatic);

        [SerializeField] private OutlineRendererFeature_Settings m_Settings;

        private DepthNormalsRenderPass depthNormalsRenderPass;
        private Material depthNormalsMaterial = null;
        private const string k_ShaderName_DepthNormals = "Outline/DepthNormals";
        private Shader m_ShaderDepthNormals = null;
        private RTHandle depthNormalsRTHandle_Colour = null;
        private RTHandle depthNormalsRTHandle_Depth = null;
        private RenderTextureDescriptor depthNormalsRTDescriptor_Colour;
        private RenderTextureDescriptor depthNormalsRTDescriptor_Depth;

        private OutlineRenderPass outlineRenderPass;
        private Material outlineMaterial = null;
        private const string k_ShaderName_Outline = "Avatar/Outline";
        private Shader m_ShaderOutline = null;
        private RTHandle outlineRTHandle = null;
        private RenderTextureDescriptor outlineRTDescriptor;

        public OutlineRendererFeature()
        {
            m_Settings = new OutlineRendererFeature_Settings();
        }

        public override void Create()
        {
            this.depthNormalsRenderPass = new DepthNormalsRenderPass();
            this.depthNormalsRenderPass.renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;

            this.outlineRenderPass = new OutlineRenderPass();
            this.outlineRenderPass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        public override void SetupRenderPasses(ScriptableRenderer _renderer, in RenderingData _renderingData)
        {
            if (this.depthNormalsMaterial == null)
            {
                this.m_ShaderDepthNormals = Shader.Find(k_ShaderName_DepthNormals);
                if (this.m_ShaderDepthNormals == null)
                {
                    ReportHub.LogError(this.m_ReportData, $"m_ShaderDepthNormals not found.");
                    return;
                }

                this.depthNormalsMaterial = CoreUtils.CreateEngineMaterial(this.m_ShaderDepthNormals);
                if (this.depthNormalsMaterial == null)
                {
                    ReportHub.LogError(this.m_ReportData, $"depthNormalsMaterial not found.");
                    return;
                }
            }

            // DepthNormals - Colour Texture
            {
                RenderTextureDescriptor desc = new RenderTextureDescriptor();
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
                this.depthNormalsRTDescriptor_Colour = desc;
            }

            // DepthNormals - Depth Texture
            {
                RenderTextureDescriptor desc = new RenderTextureDescriptor();
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
                this.depthNormalsRTDescriptor_Depth = desc;
            }

            //this.depthNormalsRTDescriptor.colorFormat = RenderTextureFormat.ARGB32;
            //depthNormalsRTDescriptor.graphicsFormat == GraphicsFormat.R8G8B8A8_UNorm;
            //this.depthNormalsRTDescriptor.depthBufferBits = 32;
            RenderingUtils.ReAllocateIfNeeded(ref depthNormalsRTHandle_Colour, depthNormalsRTDescriptor_Colour, FilterMode.Point, TextureWrapMode.Clamp, isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_DepthNormals_ColourTexture");
            RenderingUtils.ReAllocateIfNeeded(ref depthNormalsRTHandle_Depth, depthNormalsRTDescriptor_Depth, FilterMode.Point, TextureWrapMode.Clamp, isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_DepthNormals_DepthTexture");
            this.depthNormalsRenderPass.Setup(depthNormalsMaterial, depthNormalsRTHandle_Colour, depthNormalsRTDescriptor_Colour, depthNormalsRTHandle_Depth, depthNormalsRTDescriptor_Depth);

            if (this.outlineMaterial == null)
            {
                this.m_ShaderOutline = Shader.Find(k_ShaderName_Outline);
                if (this.m_ShaderOutline == null)
                {
                    ReportHub.LogError(this.m_ReportData, $"m_ShaderOutline not found.");
                    return;
                }

                this.outlineMaterial = CoreUtils.CreateEngineMaterial(this.m_ShaderOutline);
                if (this.outlineMaterial == null)
                {
                    ReportHub.LogError(this.m_ReportData, $"outlineMaterial not found.");
                    return;
                }
            }
            this.outlineRTDescriptor = _renderingData.cameraData.cameraTargetDescriptor;
            this.outlineRTDescriptor.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref this.outlineRTHandle, this.outlineRTDescriptor, FilterMode.Point, TextureWrapMode.Clamp, isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_OutlineTexture");
            this.outlineRenderPass.Setup(this.m_Settings, this.outlineMaterial, this.outlineRTHandle, this.outlineRTDescriptor, this.depthNormalsRTHandle_Colour);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(this.depthNormalsRenderPass);
            renderer.EnqueuePass(this.outlineRenderPass);
        }

        protected override void Dispose(bool disposing)
        {
            depthNormalsRenderPass?.Dispose();
            outlineRenderPass?.Dispose();

            depthNormalsRTHandle_Colour?.Release();
            depthNormalsRTHandle_Depth?.Release();
            outlineRTHandle?.Release();
        }
    }
}
