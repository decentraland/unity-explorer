using DCL.Diagnostics;
using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Utility.ComputeShaders;

namespace DCL.SkyBox.Rendering
{
    public partial class DCL_RenderFeature_ProceduralSkyBox
    {
        // Shaders
        private const string k_ShaderName_SkyBox_Generate = "DCL/SkyBox_Procedural_Generate";
        private const string k_ShaderName_StarBox_Generate = "DCL/StarBox_Procedural_Generate";
        private const string k_ShaderName_Draw = "DCL/DCL_SkyBox_Procedural_Draw";

        // Debug
        private static readonly ReportData m_ReportData = new ("DCL_RenderFeature_ProceduralSkyBox", ReportHint.SessionStatic);

        // Pass Settings
        [SerializeField] private ProceduralSkyBoxSettings_Generate m_SettingsGenerate;
        [SerializeField] private ProceduralSkyBoxSettings_Draw m_SettingsDraw;
        [SerializeField] [HideInInspector] private Shader m_ShaderSkyBoxGenerate;
        [SerializeField] [HideInInspector] private Shader m_ShaderStarBoxGenerate;
        [SerializeField] [HideInInspector] private Shader m_ShaderDraw;

        public readonly TimeOfDayRenderingModel RenderingModel;

        private readonly int nDimensions_StarBox_Array = 4096;
        private readonly int nDimensions_SkyBox_Cubemap = 1024;
        private readonly int nDimensions_StarBox_Cubemap = 4096;

        //private int nArraySize = 6;
        private ComputeShader StarsComputeShader;
        private ComputeShader m_Current_Compute_Shader_Asset;

        // Materials
        private Material m_Material_SkyBox_Generate;
        private Material m_Material_StarBox_Generate;
        private Material m_Material_Draw;

        // Passes
        private DCL_RenderPass_GenerateSkyBox m_GeneratePass;
        private DCL_RenderPass_DrawSkyBox m_DrawPass;

        // RenderTarget Handles
        private RTHandle m_SkyBoxCubeMap_RTHandle;
        private RTHandle m_StarBoxCubeMap_RTHandle;
        private RTHandle m_CubemapTextureArray_RTHandle;

        public Cubemap m_SpaceCubemap;
        private RTHandle m_SpaceCubemap_RTHandle;

        private bool bComputeStarMap = false;

        public DCL_RenderFeature_ProceduralSkyBox()
        {
            bComputeStarMap = false;
            m_SettingsGenerate = new ProceduralSkyBoxSettings_Generate();
            m_SettingsDraw = new ProceduralSkyBoxSettings_Draw();
            RenderingModel = new TimeOfDayRenderingModel();
        }

        public override void Create()
        {
            bComputeStarMap = false;
            if (bComputeStarMap == true)
            {
                // Create is called on Unity's validate
                if (m_Current_Compute_Shader_Asset)
                    ComputeShaderHotReload.Unsubscribe(m_Current_Compute_Shader_Asset, SetupStarsComputeShader);

                m_Current_Compute_Shader_Asset = m_SettingsGenerate.starsComputeShader;

                if (m_Current_Compute_Shader_Asset)
                {
                    StarsComputeShader = Instantiate(m_SettingsGenerate.starsComputeShader);
                    ComputeShaderHotReload.Subscribe(m_Current_Compute_Shader_Asset, SetupStarsComputeShader);
                }
            }

            // Create the Generate Pass...
            if (m_GeneratePass == null)
            {
                m_GeneratePass = new DCL_RenderPass_GenerateSkyBox(RenderingModel, bComputeStarMap);
                m_GeneratePass.renderPassEvent = RenderPassEvent.BeforeRendering; // Configures where the render pass should be injected.
            }

            // Create the Draw Pass...
            if (m_DrawPass == null)
            {
                m_DrawPass = new DCL_RenderPass_DrawSkyBox(RenderingModel, bComputeStarMap);
                m_DrawPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques; // Configures where the render pass should be injected.
            }

            GetMaterial_SkyBox_Generate();
            GetMaterial_StarBox_Generate();
            GetMaterial_Draw();
        }

        // Here you can inject one or multiple render passes in the renderer.
        // This method is called when setting up the renderer once per-camera.
        public override void AddRenderPasses(ScriptableRenderer _renderer, ref RenderingData _renderingData)
        {
            if (!GetMaterial_SkyBox_Generate())
            {
                ReportHub.LogError(m_ReportData, $"{GetType().Name}.AddRenderPasses(): Missing material. {name} render pass will not be added. Check for missing reference in the renderer resources.");
                return;
            }

            if (bComputeStarMap == true)
            {
                if (!GetMaterial_StarBox_Generate())
                {
                    ReportHub.LogError(m_ReportData, $"{GetType().Name}.AddRenderPasses(): Missing material. {name} render pass will not be added. Check for missing reference in the renderer resources.");
                    return;
                }
            }

            if (!GetMaterial_Draw())
            {
                ReportHub.LogError(m_ReportData, $"{GetType().Name}.AddRenderPasses(): Missing material. {name} render pass will not be added. Check for missing reference in the renderer resources.");
                return;
            }

            if (m_GeneratePass != null) { _renderer.EnqueuePass(m_GeneratePass); }

            if (m_DrawPass != null) { _renderer.EnqueuePass(m_DrawPass); }
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            // StarBox Array(x6) Rendertarget
            if (bComputeStarMap == true)
            {
                var desc = new RenderTextureDescriptor();
                desc.autoGenerateMips = false;
                desc.bindMS = false;
                desc.colorFormat = RenderTextureFormat.ARGB32;
                desc.depthBufferBits = 0;
                desc.depthStencilFormat = GraphicsFormat.None;
                desc.dimension = TextureDimension.Cube;
                desc.enableRandomWrite = false;
                desc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
                desc.height = nDimensions_StarBox_Array;
                desc.memoryless = RenderTextureMemoryless.None;
                desc.mipCount = 0;
                desc.msaaSamples = 1;
                desc.shadowSamplingMode = ShadowSamplingMode.None;
                desc.sRGB = false;
                desc.stencilFormat = GraphicsFormat.None;
                desc.useDynamicScale = false;
                desc.useMipMap = false;
                desc.volumeDepth = 1;
                desc.vrUsage = VRTextureUsage.None;
                desc.width = nDimensions_StarBox_Array;

                RenderingUtils.ReAllocateHandleIfNeeded(ref m_StarBoxCubeMap_RTHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, anisoLevel: 1, mipMapBias: 0F, name: "_StarBoxCubeMapTex");
            }

            // SkyBox Cubemap Rendertarget
            {
                var desc = new RenderTextureDescriptor();
                desc.autoGenerateMips = false;
                desc.bindMS = false;
                desc.colorFormat = RenderTextureFormat.ARGB32;
                desc.depthBufferBits = 0;
                desc.depthStencilFormat = GraphicsFormat.None;
                desc.dimension = TextureDimension.Cube;
                desc.enableRandomWrite = false;
                desc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
                desc.height = nDimensions_SkyBox_Cubemap;
                desc.memoryless = RenderTextureMemoryless.None;
                desc.mipCount = 0;
                desc.msaaSamples = 1;
                desc.shadowSamplingMode = ShadowSamplingMode.None;
                desc.sRGB = true;
                desc.stencilFormat = GraphicsFormat.None;
                desc.useDynamicScale = false;
                desc.useMipMap = false;
                desc.volumeDepth = 1;
                desc.vrUsage = VRTextureUsage.None;
                desc.width = nDimensions_SkyBox_Cubemap;

                RenderingUtils.ReAllocateHandleIfNeeded(ref m_SkyBoxCubeMap_RTHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, anisoLevel: 1, mipMapBias: 0F, name: "_SkyBoxCubeMapTex");
            }

            // StarBox Cubemap Rendertarget
            if (bComputeStarMap == true)
            {
                var desc = new RenderTextureDescriptor();
                desc.autoGenerateMips = false;
                desc.bindMS = false;
                desc.colorFormat = RenderTextureFormat.ARGB32;
                desc.depthBufferBits = 0;
                desc.depthStencilFormat = GraphicsFormat.None;
                desc.dimension = TextureDimension.Tex2DArray;
                desc.enableRandomWrite = true;
                desc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
                desc.height = nDimensions_StarBox_Cubemap;
                desc.memoryless = RenderTextureMemoryless.None;
                desc.mipCount = 0;
                desc.msaaSamples = 1;
                desc.shadowSamplingMode = ShadowSamplingMode.None;
                desc.sRGB = false;
                desc.stencilFormat = GraphicsFormat.None;
                desc.useDynamicScale = false;
                desc.useMipMap = false;
                desc.volumeDepth = 6;
                desc.vrUsage = VRTextureUsage.None;
                desc.width = nDimensions_StarBox_Cubemap;
                RenderingUtils.ReAllocateHandleIfNeeded(ref m_CubemapTextureArray_RTHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, anisoLevel: 1, mipMapBias: 0F, name: "_CubemapTextureArray");
            }

            // Space Cubemap Rendertarget
            {
                var desc = new RenderTextureDescriptor();
                desc.autoGenerateMips = false;
                desc.bindMS = false;
                desc.colorFormat = RenderTextureFormat.ARGB32;
                desc.depthBufferBits = 0;
                desc.depthStencilFormat = GraphicsFormat.None;
                desc.dimension = TextureDimension.Cube;
                desc.enableRandomWrite = false;
                desc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
                desc.height = nDimensions_SkyBox_Cubemap;
                desc.memoryless = RenderTextureMemoryless.None;
                desc.mipCount = 0;
                desc.msaaSamples = 1;
                desc.shadowSamplingMode = ShadowSamplingMode.None;
                desc.sRGB = false;
                desc.stencilFormat = GraphicsFormat.None;
                desc.useDynamicScale = false;
                desc.useMipMap = false;
                desc.volumeDepth = 1;
                desc.vrUsage = VRTextureUsage.None;
                desc.width = nDimensions_SkyBox_Cubemap;

                RenderingUtils.ReAllocateHandleIfNeeded(ref m_SpaceCubemap_RTHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, anisoLevel: 1, mipMapBias: 0F, name: "_SpaceCubeMapTex");
            }

            if (bComputeStarMap == true)
                if (StarsComputeShader == null)
                    StarsComputeShader = Instantiate(m_SettingsGenerate.starsComputeShader);

            m_GeneratePass.Setup(m_SettingsGenerate, m_Material_SkyBox_Generate, m_Material_StarBox_Generate, m_SkyBoxCubeMap_RTHandle, m_StarBoxCubeMap_RTHandle, StarsComputeShader, m_CubemapTextureArray_RTHandle);
            m_DrawPass.Setup(m_SettingsDraw, m_Material_Draw, renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle, m_SkyBoxCubeMap_RTHandle, m_StarBoxCubeMap_RTHandle, m_SpaceCubemap);
        }

        private void SetupStarsComputeShader(ComputeShader computeShader)
        {
            StarsComputeShader = Instantiate(m_SettingsGenerate.starsComputeShader);
            m_GeneratePass.SetStarsComputeShader(StarsComputeShader);
        }

        protected override void Dispose(bool disposing)
        {
            if (bComputeStarMap == true)
                if (m_Current_Compute_Shader_Asset)
                    ComputeShaderHotReload.Unsubscribe(m_Current_Compute_Shader_Asset, SetupStarsComputeShader);


            m_SkyBoxCubeMap_RTHandle?.Release();

            if (bComputeStarMap == true)
            {
                m_StarBoxCubeMap_RTHandle?.Release();
                m_CubemapTextureArray_RTHandle?.Release();
            }

            m_GeneratePass?.Dispose();
            m_DrawPass?.Dispose();


            CoreUtils.Destroy(m_Material_SkyBox_Generate);

            if (bComputeStarMap == true)
            {
                CoreUtils.Destroy(m_Material_StarBox_Generate);
                CoreUtils.Destroy(StarsComputeShader);
            }

            CoreUtils.Destroy(m_Material_Draw);
        }

        public override void OnCameraPreCull(ScriptableRenderer renderer, in CameraData cameraData) { }

        private bool GetMaterial_SkyBox_Generate()
        {
            if (m_Material_SkyBox_Generate != null) { return true; }

            if (m_ShaderSkyBoxGenerate == null)
            {
                m_ShaderSkyBoxGenerate = Shader.Find(k_ShaderName_SkyBox_Generate);

                if (m_ShaderSkyBoxGenerate == null)
                {
                    ReportHub.LogError(m_ReportData, "m_ShaderSkyBoxGenerate not found.");
                    return false;
                }
            }

            m_Material_SkyBox_Generate = CoreUtils.CreateEngineMaterial(m_ShaderSkyBoxGenerate);

            return m_Material_SkyBox_Generate != null;
        }

        private bool GetMaterial_StarBox_Generate()
        {
            if (bComputeStarMap == true)
            {
                if (m_Material_StarBox_Generate != null) { return true; }

                if (m_ShaderStarBoxGenerate == null)
                {
                    m_ShaderStarBoxGenerate = Shader.Find(k_ShaderName_StarBox_Generate);

                    if (m_ShaderStarBoxGenerate == null)
                    {
                        ReportHub.LogError(m_ReportData, "m_ShaderStarBoxGenerate not found.");
                        return false;
                    }
                }

                m_Material_StarBox_Generate = CoreUtils.CreateEngineMaterial(m_ShaderStarBoxGenerate);
            }

            return m_Material_StarBox_Generate != null;
        }

        private bool GetMaterial_Draw()
        {
            if (m_Material_Draw != null) { return true; }

            if (m_ShaderDraw == null)
            {
                m_ShaderDraw = Shader.Find(k_ShaderName_Draw);

                if (m_ShaderDraw == null)
                {
                    ReportHub.LogError(m_ReportData, "m_Shader_Draw not found.");
                    return false;
                }
            }

            m_Material_Draw = CoreUtils.CreateEngineMaterial(m_ShaderDraw);

            return m_Material_Draw != null;
        }
    }

    [Serializable]
    internal class ProceduralSkyBoxSettings_Generate
    {
        // Parameters
        [SerializeField] internal float SunSize = 1.0f;
        [SerializeField] internal float SunSizeConvergence = 5.0f;
        [SerializeField] internal float MoonSize = 0.06f;
        [SerializeField] internal float MoonSizeConvergence = 100.0f;
        [SerializeField] internal float AtmosphereThickness = 1.0f;
        [SerializeField] internal Color GroundColor = new (0.369f, 0.349f, 0.341f, 1.0f);
        [SerializeField] internal Color SkyTint = new (0.5f, 0.5f, 0.5f, 1.0f);
        [SerializeField] internal float Exposure = 1.3f;
        [SerializeField] internal Vector4 SunPos = new (-0.04f, -0.02f, 0.0f, 1.0f);
        [SerializeField] internal Color SunColour = new (1.0f, 1.0f, 1.0f, 1.0f);
        [SerializeField] internal ComputeShader starsComputeShader;
        [SerializeField] internal Vector4 _kDefaultScatteringWavelength = new (0.65f, 0.57f, 0.475f, 0.0f);
        [SerializeField] internal Vector4 _kVariableRangeForScatteringWavelength = new (0.15f, 0.15f, 0.15f, 0.0f);
        [SerializeField] internal float _OUTER_RADIUS = 1.025f;
        [SerializeField] internal float _kInnerRadius = 1.0f;
        [SerializeField] internal float _kInnerRadius2 = 1.0f;
        [SerializeField] internal float _kCameraHeight = 0.000001f;
        [SerializeField] internal float _kRAYLEIGH_MAX = 0.0025f;
        [SerializeField] internal float _kRAYLEIGH_POW = 2.5f;
        [SerializeField] internal float _kMIE = 0.0010f;
        [SerializeField] internal float _kSUN_BRIGHTNESS = 20.0f;
        [SerializeField] internal float _kMAX_SCATTER = 50.0f;
        [SerializeField] internal float _kHDSundiskIntensityFactor = 15.0f;
        [SerializeField] internal float _kSimpleSundiskIntensityFactor = 27.0f;
        [SerializeField] internal float _kSunScale_Multiplier = 400.0f;
        [SerializeField] internal float _kKm4PI_Multi = 4.0f;
        [SerializeField] internal float _kScaleDepth = 0.25f;
        [SerializeField] internal float _kScaleOverScaleDepth_Multi = 0.25f;
        [SerializeField] internal float _kSamples = 2.0f;
        [SerializeField] internal float _MIE_G = -0.990f;
        [SerializeField] internal float _MIE_G2 = 0.9801f;
        [SerializeField] internal float _SKY_GROUND_THRESHOLD = 0.02f;
    }

    [Serializable]
    internal class ProceduralSkyBoxSettings_Draw
    {
        // Parameters
    }
}
