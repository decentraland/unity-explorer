using Diagnostics.ReportsHandling;
using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
internal class ProceduralSkyBoxSettings_Generate
{
    // Parameters
    [SerializeField] internal float SunSize = 1.0f;
    [SerializeField] internal float SunSizeConvergence = 5.0f;
    [SerializeField] internal float AtmosphereThickness = 1.0f;
    [SerializeField] internal Color GroundColor = new (0.369f, 0.349f, 0.341f, 1.0f);
    [SerializeField] internal Color SkyTint = new (0.5f, 0.5f, 0.5f, 1.0f);
    [SerializeField] internal float Exposure = 1.3f;
    [SerializeField] internal Vector4 SunPos = new (-0.04f, -0.02f, 0.0f, 1.0f);
    [SerializeField] internal Vector4 SunColour = new (1.0f, 1.0f, 1.0f, 1.0f);
}

[Serializable]
internal class ProceduralSkyBoxSettings_Draw
{
    // Parameters
}

public partial class DCL_RenderFeature_ProceduralSkyBox : ScriptableRendererFeature
{
    // Shaders
    private const string k_ShaderName_Generate = "CustomRenderTexture/SkyBox_Procedural_Generate";
    private const string k_ShaderName_Draw = "Skybox/DCL_SkyBox_Procedural_Draw";

    // Debug
    private readonly ReportData m_ReportData = new ("DCL_RenderFeature_ProceduralSkyBox", ReportHint.SessionStatic);

    // Pass Settings
    [SerializeField] private ProceduralSkyBoxSettings_Generate m_SettingsGenerate;
    [SerializeField] private ProceduralSkyBoxSettings_Draw m_SettingsDraw;
    [SerializeField] [HideInInspector] private Shader m_ShaderGenerate;
    [SerializeField] [HideInInspector] private Shader m_ShaderDraw;

    // Materials
    private Material m_Material_Generate;
    private Material m_Material_Draw;

    // Passes
    private DCL_RenderPass_GenerateSkyBox m_GeneratePass;
    private DCL_RenderPass_DrawSkyBox m_DrawPass;

    // RenderTarget Handles
    private RTHandle m_SkyBoxCubeMap_RTHandle;

    public DCL_RenderFeature_ProceduralSkyBox()
    {
        m_SettingsGenerate = new ProceduralSkyBoxSettings_Generate();
        m_SettingsDraw = new ProceduralSkyBoxSettings_Draw();
    }

    public override void Create()
    {
        // Create the Generate Pass...
        if (m_GeneratePass == null)
        {
            m_GeneratePass = new DCL_RenderPass_GenerateSkyBox();
            m_GeneratePass.renderPassEvent = RenderPassEvent.BeforeRendering; // Configures where the render pass should be injected.
        }

        // Create the Draw Pass...
        if (m_DrawPass == null)
        {
            m_DrawPass = new DCL_RenderPass_DrawSkyBox();
            m_DrawPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques; // Configures where the render pass should be injected.
        }

        GetMaterial_Generate();
        GetMaterial_Draw();
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer _renderer, ref RenderingData _renderingData)
    {
        if (!GetMaterial_Generate())
        {
            ReportHub.LogError(m_ReportData, $"{GetType().Name}.AddRenderPasses(): Missing material. {name} render pass will not be added. Check for missing reference in the renderer resources.");
            return;
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
        var desc = new RenderTextureDescriptor();
        desc.autoGenerateMips = false;
        desc.bindMS = false;
        desc.colorFormat = RenderTextureFormat.ARGB32;
        desc.depthBufferBits = 0;
        desc.depthStencilFormat = GraphicsFormat.None;
        desc.dimension = TextureDimension.Cube;
        desc.enableRandomWrite = false;
        desc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
        desc.height = 1024;
        desc.memoryless = RenderTextureMemoryless.None;
        desc.mipCount = 0;
        ;
        desc.msaaSamples = 1;
        desc.shadowSamplingMode = ShadowSamplingMode.None;
        desc.sRGB = false;
        desc.stencilFormat = GraphicsFormat.None;
        desc.useDynamicScale = false;
        desc.useMipMap = false;
        desc.volumeDepth = 0;
        desc.vrUsage = VRTextureUsage.None;
        desc.width = 1024;

        RenderingUtils.ReAllocateIfNeeded(ref m_SkyBoxCubeMap_RTHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_SkyBoxCubeMapTex");

        m_GeneratePass.Setup(m_SettingsGenerate, m_Material_Generate, m_SkyBoxCubeMap_RTHandle);
        m_DrawPass.Setup(m_SettingsDraw, m_Material_Draw, renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle, m_SkyBoxCubeMap_RTHandle);
    }

    protected override void Dispose(bool disposing)
    {
        m_SkyBoxCubeMap_RTHandle?.Release();
        m_GeneratePass?.dispose();
        m_DrawPass?.dispose();
        CoreUtils.Destroy(m_Material_Generate);
        CoreUtils.Destroy(m_Material_Draw);
    }

    public override void OnCameraPreCull(ScriptableRenderer renderer, in CameraData cameraData) { }

    private bool GetMaterial_Generate()
    {
        if (m_Material_Generate != null) { return true; }

        if (m_ShaderGenerate == null)
        {
            m_ShaderGenerate = Shader.Find(k_ShaderName_Generate);

            if (m_ShaderGenerate == null)
            {
                ReportHub.LogError(m_ReportData, "m_Shader_Generate not found.");
                return false;
            }
        }

        m_Material_Generate = CoreUtils.CreateEngineMaterial(m_ShaderGenerate);

        return m_Material_Generate != null;
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
