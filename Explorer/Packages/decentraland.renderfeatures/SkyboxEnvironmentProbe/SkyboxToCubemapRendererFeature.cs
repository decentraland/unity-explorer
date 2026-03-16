using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SkyboxToCubemapRendererFeature : ScriptableRendererFeature
{
    public SkyBoxToCubemapSettings settings = new ();
    
    private RenderTextureDescriptor desc;
    private RenderTextureDescriptor descScratch;
    
    private SkyboxToCubemapRenderPass skyboxToCubemapRenderPass;
    
    private Material currentSkyboxMaterial;
    private Material instancedSkyboxMaterial;
    private Material cubeCopyMaterial;
    private Material cubeBlurMaterial;
    
    private RTHandle skyBoxCubeMapRTHandle;
    private RTHandle skyBoxCubeMapRTHandle_Scratch;

    /// <inheritdoc/>
    public override void Create()
    {
#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            return;
#endif
        
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            return;
        
        // Create is called on enable and on validate (from Editor)
        if (settings.skyBoxShader == null)
            return;
        
        if (!Mathf.IsPowerOfTwo(settings.dimensions)) return;
        
        CoreUtils.Destroy(instancedSkyboxMaterial); // destroy previously created material
        instancedSkyboxMaterial = new Material(settings.skyBoxShader);

        CoreUtils.Destroy(cubeCopyMaterial); // destroy previously created material
        cubeCopyMaterial = new Material(Shader.Find("DCL/CubeCopy"));

        CoreUtils.Destroy(cubeBlurMaterial); // destroy previously created material
        cubeBlurMaterial = new Material(Shader.Find("DCL/CubeBlur"));
        
        
        // If render texture was created but no longer should be executed in the editor release it
        if (SkipInEditorMode())
        {
            if (skyBoxCubeMapRTHandle != null)
            {
                skyBoxCubeMapRTHandle.Release();
                skyBoxCubeMapRTHandle = null;
            }

            if (skyBoxCubeMapRTHandle_Scratch != null)
            {
                skyBoxCubeMapRTHandle_Scratch.Release();
                skyBoxCubeMapRTHandle_Scratch = null;
            }

            return;
        }
        
        // Create renderTexture cubeMap
        desc = new RenderTextureDescriptor();
        desc.autoGenerateMips = false;
        desc.bindMS = false;
        desc.colorFormat = RenderTextureFormat.RGB111110Float;
        desc.depthBufferBits = 0;
        desc.depthStencilFormat = GraphicsFormat.None;
        desc.dimension = TextureDimension.Cube;
        desc.enableRandomWrite = false;
        desc.graphicsFormat = GraphicsFormat.B10G11R11_UFloatPack32;
        desc.height = settings.dimensions;
        desc.width = settings.dimensions;
        desc.memoryless = RenderTextureMemoryless.None;
        desc.mipCount = 9;
        desc.msaaSamples = 1;
        desc.shadowSamplingMode = ShadowSamplingMode.None;
        desc.sRGB = false;
        desc.stencilFormat = GraphicsFormat.None;
        desc.useDynamicScale = false;
        desc.useMipMap = true;
        desc.volumeDepth = 1;
        desc.vrUsage = VRTextureUsage.None;

        // Just copy it, since its the same.
        descScratch = desc;
        
        RenderingUtils.ReAllocateHandleIfNeeded (ref skyBoxCubeMapRTHandle, desc, FilterMode.Trilinear, TextureWrapMode.Clamp, anisoLevel: 1, mipMapBias: 0F, name: "_SkyBoxCubeMapTex");
        RenderingUtils.ReAllocateHandleIfNeeded (ref skyBoxCubeMapRTHandle_Scratch, descScratch, FilterMode.Trilinear, TextureWrapMode.Clamp, anisoLevel: 1, mipMapBias: 0F, name: "_SkyBoxCubeMapTex_scratch");
        
        skyboxToCubemapRenderPass = new SkyboxToCubemapRenderPass(instancedSkyboxMaterial, cubeCopyMaterial, cubeBlurMaterial)
        {
            skyBoxCubeMapRTHandle = skyBoxCubeMapRTHandle,
            skyBoxCubeMapRTHandle_Scratch = skyBoxCubeMapRTHandle_Scratch,
            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques,
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (SkipInEditorMode())
            return;
        
        if (renderingData.cameraData.cameraType is CameraType.Preview or CameraType.SceneView
            || !renderingData.cameraData.camera.CompareTag("MainCamera"))
            return;

        if (!RenderSettings.skybox)
            return;

        if (RenderSettings.skybox != currentSkyboxMaterial)
        {
            currentSkyboxMaterial = RenderSettings.skybox;
            skyboxToCubemapRenderPass.SetSkyboxMaterial(currentSkyboxMaterial);
        }
        
        if (!instancedSkyboxMaterial)
            return;

        if (!cubeCopyMaterial)
            return;

        if (!cubeBlurMaterial)
            return;

        if (skyboxToCubemapRenderPass != null)
        {
            renderer.EnqueuePass(skyboxToCubemapRenderPass);
        }
        
        if (skyBoxCubeMapRTHandle != null)
        {
            if (settings.assignAsReflectionProbe)
            {
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = skyBoxCubeMapRTHandle;
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        skyBoxCubeMapRTHandle?.Release();
        skyBoxCubeMapRTHandle = null;
        
        skyBoxCubeMapRTHandle_Scratch?.Release();
        skyBoxCubeMapRTHandle_Scratch = null;

        CoreUtils.Destroy(instancedSkyboxMaterial);
        instancedSkyboxMaterial = null;
        
        CoreUtils.Destroy(cubeCopyMaterial);
        cubeCopyMaterial = null;

        CoreUtils.Destroy(cubeBlurMaterial);
        cubeBlurMaterial = null;
        
        skyboxToCubemapRenderPass = null;
    }

    private bool SkipInEditorMode() =>
        !settings.executeInEditMode && Application.isEditor && !Application.isPlaying;

    [Serializable]
    public class SkyBoxToCubemapSettings
    {
        public Shader skyBoxShader;
        public int dimensions = 256;
        public bool executeInEditMode;
        public bool assignAsReflectionProbe;
    }
}
