using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

[Serializable]
internal class ProceduralSkyBoxSettings_Generate
{
    // Parameters
    //[SerializeField] internal Vector2 Wearline = new Vector2(0.67f, 0.59f);
    [SerializeField] internal int SunDisk = 2;
    [SerializeField] internal float SunSize = 1.0f;
    [SerializeField] internal float SunSizeConvergence = 5.0f;
    [SerializeField] internal float AtmosphereThickness = 1.0f;
    [SerializeField] internal Color GroundColor = new Color(0.369f, 0.349f, 0.341f, 1.0f);
    [SerializeField] internal Color SkyTint = new Color(0.5f, 0.5f, 0.5f, 1.0f);
    [SerializeField] internal float Exposure = 1.3f;
}

[Serializable]
internal class ProceduralSkyBoxSettings_Draw
{
    // Parameters
    //[SerializeField] internal Vector2 Wearline = new Vector2(0.67f, 0.59f);
    [SerializeField] internal int SunDisk = 2;
    [SerializeField] internal float SunSize = 1.0f;
    [SerializeField] internal float SunSizeConvergence = 5.0f;
    [SerializeField] internal float AtmosphereThickness = 1.0f;
    [SerializeField] internal Color GroundColor = new Color(0.369f, 0.349f, 0.341f, 1.0f);
    [SerializeField] internal Color SkyTint = new Color(0.5f, 0.5f, 0.5f, 1.0f);
    [SerializeField] internal float Exposure = 1.3f;
}

public partial class DCL_RenderFeature_ProceduralSkyBox : ScriptableRendererFeature
{
    // Pass Settings
    [SerializeField] private ProceduralSkyBoxSettings_Generate m_Settings_Generate;
    [SerializeField] private ProceduralSkyBoxSettings_Draw m_Settings_Draw;

    // Shaders
    private const string k_ShaderName_Generate = "CustomRenderTexture/SkyBox_Procedural_Generate";
    [SerializeField, HideInInspector] private Shader m_Shader_Generate = null;
    private const string k_ShaderName_Draw = "Skybox/DCL_SkyBox_Procedural_Draw";
    [SerializeField, HideInInspector] private Shader m_Shader_Draw = null;

    // Materials
    private Material m_Material_Generate;
    private Material m_Material_Draw;

    // Passes
    DCL_RenderPass_GenerateSkyBox m_GeneratePass;
    DCL_RenderPass_DrawSkyBox m_DrawPass;
   
    // RenderTarget Handles
    RTHandle m_SkyBoxCubeMap_RTHandle;

    public DCL_RenderFeature_ProceduralSkyBox()
    {
        m_Settings_Generate = new ProceduralSkyBoxSettings_Generate();
        m_Settings_Draw = new ProceduralSkyBoxSettings_Draw();
    }

    public override void Create()
    {
        Debug.Log("DCL_RenderFeature_ProceduralSkyBox::Create");
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
        Debug.Log("DCL_RenderFeature_ProceduralSkyBox::AddRenderPasses");
        if (!GetMaterial_Generate())
        {
            Debug.LogErrorFormat("{0}.AddRenderPasses(): Missing material. {1} render pass will not be added. Check for missing reference in the renderer resources.", GetType().Name, name);
            return;
        }

        if (!GetMaterial_Draw())
        {
            Debug.LogErrorFormat("{0}.AddRenderPasses(): Missing material. {1} render pass will not be added. Check for missing reference in the renderer resources.", GetType().Name, name);
            return;
        }


        if (m_GeneratePass != null)
        {
            _renderer.EnqueuePass(m_GeneratePass);
        }


        if (m_DrawPass != null)
        {
            _renderer.EnqueuePass(m_DrawPass);
        }
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        Debug.Log("DCL_RenderFeature_ProceduralSkyBox::SetupRenderPasses");
        RenderTextureDescriptor desc = new RenderTextureDescriptor();
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
        desc.mipCount = 0;;
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

        m_GeneratePass.Setup(m_Settings_Generate, m_Material_Generate, m_SkyBoxCubeMap_RTHandle);
        m_DrawPass.Setup(m_Settings_Draw, m_Material_Draw, renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle, m_SkyBoxCubeMap_RTHandle);
    }

    protected override void Dispose(bool disposing)
    {
        Debug.Log("DCL_RenderFeature_ProceduralSkyBox::Dispose");
        m_SkyBoxCubeMap_RTHandle?.Release();
        m_GeneratePass.dispose();
        m_DrawPass.dispose();
        CoreUtils.Destroy(m_Material_Generate);
        CoreUtils.Destroy(m_Material_Draw);
    }

    public override void OnCameraPreCull(ScriptableRenderer renderer, in CameraData cameraData)
    {
        Debug.Log("DCL_RenderFeature_ProceduralSkyBox::OnCameraPreCull");
    }

    // public bool isActive { get; }
    // public void SetActive(bool active) {}

    // private bool GetMaterialPropertyBlock_Generate()
    // {
    //     //MaterialPropertyBlock
    //     return false;
    // }

    private bool GetMaterial_Generate()
    {
        Debug.Log("DCL_RenderFeature_ProceduralSkyBox::GetMaterial_Generate");
        if (m_Material_Generate != null)
        {
            Debug.Log("DCL_RenderFeature_ProceduralSkyBox::GetMaterial_Generate == return true");
            return true;
        }

        if (m_Shader_Generate == null)
        {
            Debug.Log("DCL_RenderFeature_ProceduralSkyBox::GetMaterial_Generate == Find shader");
            m_Shader_Generate = Shader.Find(k_ShaderName_Generate);
            if (m_Shader_Generate == null)
            {
                Debug.Log("m_Shader_Generate not found");
                return false;
            }
        }

        m_Material_Generate = CoreUtils.CreateEngineMaterial(m_Shader_Generate);

        return m_Material_Generate != null;
    }

    private bool GetMaterial_Draw()
    {
        Debug.Log("DCL_RenderFeature_ProceduralSkyBox::GetMaterial_Draw");
        if (m_Material_Draw != null)
        {
            Debug.Log("DCL_RenderFeature_ProceduralSkyBox::GetMaterial_Draw == return true");
            return true;
        }

        if (m_Shader_Draw == null)
        {
            Debug.Log("DCL_RenderFeature_ProceduralSkyBox::GetMaterial_Draw == Find shader");
            m_Shader_Draw = Shader.Find(k_ShaderName_Draw);
            if (m_Shader_Draw == null)
            {
                Debug.Log("m_Shader_Draw not found");
                return false;
            }
        }

        m_Material_Draw = CoreUtils.CreateEngineMaterial(m_Shader_Draw);

        return m_Material_Draw != null;
    }
}


